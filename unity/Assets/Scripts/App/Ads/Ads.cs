using System;
using UnityEngine;
#if SISIFOS_ADS
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace Sisifos.App
{
    /// <summary>
    /// The owner's AdMob ad unit ids, under the game's own AdMob apps ("Sisyphus: Sonsuz
    /// Tırmanış" on Android, linked to com.lotusdugun.sisyphus on Google Play; "Sisyphus:
    /// Endless Climb" on iOS, to be linked to the App Store once the game is there). The App
    /// IDs (ca-app-pub-…~…) live in Assets/Editor/LogExport/AdsSetup.cs.
    /// </summary>
    public static class AdIds
    {
#if UNITY_IOS
        public const string Rewarded = "ca-app-pub-5494310464478030/2831160892";
        public const string Banner = "ca-app-pub-5494310464478030/3884835223";
        public const string Interstitial = "ca-app-pub-5494310464478030/4775245062";
#else
        public const string Rewarded = "ca-app-pub-5494310464478030/2710907241";
        public const string Banner = "ca-app-pub-5494310464478030/4023988918";
        public const string Interstitial = "ca-app-pub-5494310464478030/7824080236";
#endif
    }

    /// <summary>
    /// Ads: a banner under the main menu, the Book of Wisdom and the settings, and a small one
    /// during the climb beside the cycle count, clear of the controls (only where it fits); a full-screen ad now and then
    /// between climbs, as he sets his shoulder to the stone again (every eighth climb at
    /// most, and never twice within ten minutes); and one rewarded ad the player
    /// chooses — when his breath gives out, a "second wind" once a climb. Asks for consent first (Google's UMP form, where the law wants
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

        /// <summary>show the banner along the bottom (the menu is up) or hide it (anything else)</summary>
        public static void Banner(bool show)
        {
            SetBanner(show ? 1 : 0, 0, 0);
        }

        /// <summary>
        /// the banner during the climb, its top-left at (x, y) in UI units (dp): beside the cycle
        /// count, kept clear of the joystick and the throw button by the caller
        /// </summary>
        public static void BannerAt(float x, float y)
        {
            SetBanner(2, (int)Math.Round(x), (int)Math.Round(y));
        }

        /// <summary>a banner is loaded and can be shown (an ad came back for it)</summary>
        public static bool BannerLoaded { get; private set; }

        /// <summary>0 hidden, 1 along the bottom, 2 at (atX, atY)</summary>
        static int bannerMode;
        static int atX, atY;

        static void SetBanner(int mode, int x, int y)
        {
            if (mode == bannerMode && (mode != 2 || (x == atX && y == atY))) return;
            bannerMode = mode;
            atX = x; atY = y;
            bannerWanted = mode != 0;
            BannerHeight = 0;
#if SISIFOS_ADS
            if (!Ready) return;
            try { ShowBanner(); }
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

        /// <summary>
        /// Between climbs: a full-screen ad if one is loaded, at most every `everyCycles`
        /// climbs and `minGapSeconds` apart, never in the first climbs. `cycles` is the count
        /// of finished climbs.
        /// </summary>
        public static void MaybeInterstitial(double cycles, int everyCycles = 8, double minGapSeconds = 600)
        {
#if SISIFOS_ADS
            if (!Ready || interstitial == null || !interstitial.CanShowAd()) return;
            if (cycles < everyCycles) return;
            if (lastFullCycles >= 0 && cycles - lastFullCycles < everyCycles) return;
            if (Time.realtimeSinceStartup - lastFullAt < minGapSeconds && lastFullAt > 0) return;
            var ad = interstitial;
            interstitial = null;
            lastFullCycles = cycles;
            lastFullAt = Time.realtimeSinceStartup;
            bool finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                try { ad.Destroy(); } catch (Exception) { }
                LoadInterstitial();
            }
            ad.OnAdFullScreenContentClosed += () => Finish();
            ad.OnAdFullScreenContentFailed += (AdError e) => { Debug.Log("[Ads] interstitial failed: " + e.GetMessage()); Finish(); };
            try { ad.Show(); }
            catch (Exception e) { Debug.Log("[Ads] interstitial show: " + e.Message); Finish(); }
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
        static InterstitialAd interstitial;
        static double lastFullCycles = -1;
        static float lastFullAt;
        static float retryFull = 1;
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
                LoadInterstitial();
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

        static void LoadInterstitial()
        {
            if (!Ready) return;
            InterstitialAd.Load(AdIds.Interstitial, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.Log("[Ads] interstitial load: " + (error != null ? error.GetMessage() : "no ad"));
                    retryFull = Mathf.Min(120, retryFull * 2);
                    if (GameRoot.instance != null) GameRoot.instance.StartCoroutine(After(retryFull, LoadInterstitial));
                    return;
                }
                retryFull = 1;
                interstitial = ad;
            });
        }

        static System.Collections.IEnumerator After(float seconds, Action f)
        {
            yield return new WaitForSecondsRealtime(seconds);
            f();
        }

        static void ShowBanner()
        {
            if (string.IsNullOrEmpty(AdIds.Banner)) { BannerHeight = 0; return; }
            if (bannerMode == 0)
            {
                if (banner != null) banner.Hide();
                BannerHeight = 0;
                return;
            }
            if (banner == null)
            {
                banner = new BannerView(AdIds.Banner, AdSize.Banner, AdPosition.Bottom);
                banner.OnBannerAdLoaded += () => BannerLoaded = true;
                banner.OnBannerAdLoadFailed += (LoadAdError e) => { BannerLoaded = false; Debug.Log("[Ads] banner failed: " + e.GetMessage()); };
                banner.LoadAd(new AdRequest());
            }
            if (bannerMode == 1) banner.SetPosition(AdPosition.Bottom);
            else banner.SetPosition(atX, atY);
            banner.Show();
            // AdSize.Banner is 50 dp tall; the UI's units are CSS pixels, which are dp. Only the
            // bottom banner takes room from the page above it; the one in the climb floats
            BannerHeight = bannerMode == 1 ? 50 : 0;
        }
#endif
    }
}
