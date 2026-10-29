using System;
using UnityEngine;
#if SISIFOS_ADS
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace Sisifos.App
{
    /// <summary>
    /// The ad unit ids. Until the owner's own are written in, these are Google's public TEST
    /// ids: they show "Test Ad" placeholders and earn nothing, and are safe to click.
    /// The App IDs (ca-app-pub-…~…) live in Assets/Editor/LogExport/AdsSetup.cs, which
    /// writes them into the plugin's settings.
    /// </summary>
    public static class AdIds
    {
#if UNITY_IOS
        public const string Rewarded = "ca-app-pub-3940256099942544/1712485313";
        public const string Banner = "ca-app-pub-3940256099942544/2934735716";
#else
        public const string Rewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string Banner = "ca-app-pub-3940256099942544/6300978111";
#endif
    }

    /// <summary>
    /// Ads, kept small and out of the way: a banner under the main menu only (never over the
    /// climb), and one rewarded ad the player chooses — when his breath gives out, a "second
    /// wind" once a climb. Asks for consent first (Google's UMP form, where the law wants
    /// one). Without the Google Mobile Ads plugin in the project (SISIFOS_ADS unset), and in
    /// the web build, everything here is a quiet no-op and nothing is offered.
    /// </summary>
    public static class Ads
    {
        /// <summary>the plugin is in and ads may be requested (consent given or not needed)</summary>
        public static bool Ready { get; private set; }
        /// <summary>a rewarded ad is loaded and can be shown now</summary>
        public static bool RewardedReady
        {
            get
            {
#if SISIFOS_ADS
                return Ready && rewarded != null && rewarded.CanShowAd();
#else
                return false;
#endif
            }
        }
        /// <summary>the banner's height on screen while it shows, in UI units (0 when hidden)</summary>
        public static float BannerHeight { get; private set; }
        /// <summary>the privacy options form must be reachable from the settings</summary>
        public static bool PrivacyOptionsRequired
        {
            get
            {
#if SISIFOS_ADS
                return Ready && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
#else
                return false;
#endif
            }
        }

        static bool started;
        static bool bannerWanted;

        /// <summary>once, at start: consent, then the SDK, then the first rewarded ad</summary>
        public static void Init()
        {
            if (started) return;
            started = true;
#if SISIFOS_ADS
            try
            {
                // callbacks on Unity's main thread, where the UI can be touched
                MobileAds.RaiseAdEventsOnUnityMainThread = true;
                var req = new ConsentRequestParameters();
                ConsentInformation.Update(req, (FormError err) =>
                {
                    if (err != null) Debug.Log("[Ads] consent info: " + err.Message);
                    ConsentForm.LoadAndShowConsentFormIfRequired((FormError e2) =>
                    {
                        if (e2 != null) Debug.Log("[Ads] consent form: " + e2.Message);
                        if (ConsentInformation.CanRequestAds()) StartSdk();
                    });
                });
                // consent from an earlier run: no need to wait for the update
                if (ConsentInformation.CanRequestAds()) StartSdk();
            }
            catch (Exception e)
            {
                Debug.Log("[Ads] init failed: " + e.Message);
            }
#endif
        }

        /// <summary>show the banner (the menu is up) or hide it (anything else)</summary>
        public static void Banner(bool show)
        {
            if (bannerWanted == show) return;
            bannerWanted = show;
#if SISIFOS_ADS
            if (!Ready) return;
            try
            {
                if (show) ShowBanner();
                else if (banner != null) { banner.Hide(); BannerHeight = 0; }
            }
            catch (Exception e) { Debug.Log("[Ads] banner: " + e.Message); }
#endif
        }

        /// <summary>show the rewarded ad; `done(true)` once the reward was earned and the ad closed</summary>
        public static void ShowRewarded(Action<bool> done)
        {
#if SISIFOS_ADS
            if (!RewardedReady) { done?.Invoke(false); return; }
            var ad = rewarded;
            rewarded = null;
            bool earned = false;
            bool finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                try { ad.Destroy(); } catch (Exception) { }
                LoadRewarded();
                done?.Invoke(earned);
            }
            ad.OnAdFullScreenContentClosed += () => Finish();
            ad.OnAdFullScreenContentFailed += (AdError e) => { Debug.Log("[Ads] rewarded failed: " + e.GetMessage()); Finish(); };
            try
            {
                ad.Show((Reward r) => { earned = true; });
            }
            catch (Exception e)
            {
                Debug.Log("[Ads] rewarded show: " + e.Message);
                Finish();
            }
#else
            done?.Invoke(false);
#endif
        }

        /// <summary>the privacy options form (settings): change or withdraw consent</summary>
        public static void ShowPrivacyOptions()
        {
#if SISIFOS_ADS
            try
            {
                ConsentForm.ShowPrivacyOptionsForm((FormError e) =>
                {
                    if (e != null) Debug.Log("[Ads] privacy options: " + e.Message);
                });
            }
            catch (Exception e) { Debug.Log("[Ads] privacy options: " + e.Message); }
#endif
        }

#if SISIFOS_ADS
        static RewardedAd rewarded;
        static BannerView banner;
        static bool sdkStarted;
        static float retry = 1;

        static void StartSdk()
        {
            if (sdkStarted) return;
            sdkStarted = true;
            MobileAds.Initialize((InitializationStatus status) =>
            {
                Ready = true;
                LoadRewarded();
                if (bannerWanted) ShowBanner();
            });
        }

        static void LoadRewarded()
        {
            if (!Ready) return;
            RewardedAd.Load(AdIds.Rewarded, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.Log("[Ads] rewarded load: " + (error != null ? error.GetMessage() : "no ad"));
                    // try again later, backing off up to a couple of minutes
                    retry = Mathf.Min(120, retry * 2);
                    if (GameRoot.instance != null) GameRoot.instance.StartCoroutine(After(retry, LoadRewarded));
                    return;
                }
                retry = 1;
                rewarded = ad;
            });
        }

        static System.Collections.IEnumerator After(float seconds, Action f)
        {
            yield return new WaitForSecondsRealtime(seconds);
            f();
        }

        static void ShowBanner()
        {
            if (banner == null)
            {
                banner = new BannerView(AdIds.Banner, AdSize.Banner, AdPosition.Bottom);
                banner.LoadAd(new AdRequest());
            }
            banner.Show();
            // AdSize.Banner is 50 dp tall; the UI's units are CSS pixels, which are dp
            BannerHeight = 50;
        }
#endif
    }
}
