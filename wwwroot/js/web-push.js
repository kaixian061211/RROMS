(() => {
    const config = document.getElementById("web-push-config");
    if (!config) return;

    let sdk;
    let expectedAlias = null;
    let ready = false;
    let busy = false;
    let permissionBusy = false;
    let requested = false;

    const offered = new Set();
    const currentAlias = () => sdk.User.externalId ?? null;

    function report(error) {
        console.warn(
            "Browser notifications could not be configured.",
            error
        );
    }

    async function offerPermission() {
        if (!ready || busy || permissionBusy
            || currentAlias() !== expectedAlias
            || offered.has(expectedAlias)) {
            return;
        }

        const nativePermission = window.Notification?.permission;

        const permitted = nativePermission
            ? nativePermission === "granted"
            : sdk.Notifications.permission;

        if (nativePermission === "denied"
            || (permitted && sdk.User.PushSubscription.optedIn)) {
            return;
        }

        offered.add(expectedAlias);
        permissionBusy = true;

        try {
            if (permitted) {
                // Register an already-permitted browser.
                // Preserve an existing subscription's explicit opt-out.
                if (!sdk.User.PushSubscription.token) {
                    await sdk.User.PushSubscription.optIn();
                }
            } else {
                // The customer still chooses Allow in the browser.
                await sdk.Slidedown.promptPush();
            }
        } catch (error) {
            report(error);
        } finally {
            permissionBusy = false;

            if (requested) {
                synchronize();
            }
        }
    }

    async function getIdentity() {
        const tokenResponse = await fetch(config.dataset.tokenUrl, {
            credentials: "same-origin",
            cache: "no-store"
        });

        if (!tokenResponse.ok) {
            throw new Error("Token unavailable");
        }

        const token = await tokenResponse.json();
        const body = new URLSearchParams();

        body.set(token.formFieldName, token.requestToken);

        const response = await fetch(config.dataset.identityUrl, {
            method: "POST",
            body,
            credentials: "same-origin",
            cache: "no-store"
        });

        if (response.status === 401 || response.status === 403) {
            return null;
        }

        if (!response.ok) {
            throw new Error("Identity unavailable");
        }

        const identity = await response.json();

        if (typeof identity.externalId !== "string"
            || !identity.externalId) {
            throw new Error("Invalid identity");
        }

        return identity.externalId;
    }

    function synchronize() {
        requested = true;

        if (!sdk || busy || permissionBusy) return;

        busy = true;
        ready = false;

        const run = async () => {
            try {
                do {
                    requested = false;

                    const alias = await getIdentity();
                    if (requested) continue;

                    if (currentAlias() !== alias) {
                        if (currentAlias()) {
                            await sdk.logout();
                        }

                        if (alias) {
                            await sdk.login(alias);
                        }
                    }

                    expectedAlias = alias;

                    ready = Boolean(alias)
                        && currentAlias() === alias;
                } while (requested);
            } catch (error) {
                ready = false;
                expectedAlias = null;

                try {
                    await sdk.logout();
                } catch {
                }

                report(error);
            } finally {
                busy = false;
                void offerPermission();
            }
        };

        if (navigator.locks) {
            navigator.locks.request(
                "rroms-web-push-identity",
                run
            ).catch(error => {
                busy = false;
                ready = false;
                report(error);
            });
        } else {
            void run();
        }
    }

    window.OneSignalDeferred = window.OneSignalDeferred || [];

    window.OneSignalDeferred.push(async OneSignal => {
        try {
            await OneSignal.init({
                appId: config.dataset.appId,

                allowLocalhostAsSecureOrigin:
                    config.dataset.local === "true",

                serviceWorkerPath:
                    config.dataset.workerPath,

                serviceWorkerParam: {
                    scope: config.dataset.workerScope
                },

                notifyButton: {
                    enable: false
                },

                welcomeNotification: {
                    disable: true
                },

                promptOptions: {
                    slidedown: {
                        prompts: [{
                            type: "push",
                            autoPrompt: false
                        }]
                    }
                }
            });

            sdk = OneSignal;

            if (!sdk.Notifications?.isPushSupported?.()
                || !sdk.User?.PushSubscription) {
                return;
            }

            sdk.User.PushSubscription.addEventListener(
                "change",
                offerPermission
            );

            sdk.Notifications.addEventListener(
                "permissionChange",
                offerPermission
            );

            sdk.User.addEventListener("change", () => {
                if (busy) return;

                ready = Boolean(expectedAlias)
                    && currentAlias() === expectedAlias;

                if (currentAlias() !== expectedAlias) {
                    synchronize();
                } else {
                    void offerPermission();
                }
            });

            window.addEventListener("focus", synchronize);

            document.addEventListener("visibilitychange", () => {
                if (document.visibilityState === "visible") {
                    synchronize();
                }
            });

            synchronize();
        } catch (error) {
            report(error);
        }
    });
})();