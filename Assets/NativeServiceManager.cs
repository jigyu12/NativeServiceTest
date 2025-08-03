using Firebase;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using Firebase.Extensions;
using GoogleMobileAds.Api;
using System;
using GooglePlayGames;

public class NavtiveServiceManager : MonoBehaviour
{
    public TextMeshProUGUI text;

    private FirebaseApp firebaseApp;
    private FirebaseAuth auth;

    // AdMob 관련 변수
    private BannerView bannerView;
    private InterstitialAd interstitialAd;

    // 광고 유닛 ID (테스트 ID로 변경 필요)
    // private string bannerAdUnitId = "ca-app-pub-7621690477747234/1655431246";
    // private string interstitialAdUnitId = "ca-app-pub-7621690477747234/9342349574";
    private string bannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
    private string interstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";

    // Google Play Games 관련 ID (실제 ID로 변경 필요)
    private string leaderboardId = GPGSIds.leaderboard;
    private string achievementId = GPGSIds.achievement;

    public void Log(string log)
    {
        Debug.Log(log);
        text.text = $"{log}\n{text.text}";
    }

    private void Start()
    {
        InitializeFirebase();
        InitializeGooglePlayGames();
        InitializeAdMob();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                // Firebase 초기화 성공
                firebaseApp = FirebaseApp.DefaultInstance;
                auth = FirebaseAuth.DefaultInstance;
                Log("Firebase 초기화 성공");
            }
            else
            {
                Log($"Firebase 초기화 실패: {dependencyStatus}");
            }
        });
    }

    public void SignInWithGoogle()
    {
        if (FirebaseAuth.DefaultInstance == null)
        {
            Debug.LogError("Firebase Auth가 초기화되지 않았습니다");
            return;
        }

        FirebaseAuth.DefaultInstance.SignInWithProviderAsync(new FederatedOAuthProvider(new FederatedOAuthProviderData
        {
            ProviderId = GoogleAuthProvider.ProviderId
        })).ContinueWithOnMainThread(task => {
            if (task.IsCanceled)
            {
                Debug.LogError("Google 인증 취소됨");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError($"Google 인증 실패: {task.Exception}");
                return;
            }

            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user != null)
            {
                Debug.Log($"구글 로그인 성공: {user.DisplayName} ({user.Email})");
            }
        });
    }

    public void SignOut()
    {
        if (auth != null)
        {
            auth.SignOut();
            Debug.Log("로그아웃 완료");
        }
    }

    private void InitializeGooglePlayGames()
    {
        PlayGamesPlatform.Activate();
        Log("Google Play Games 초기화 성공");
    }

    public void SignInWithGooglePlayGames()
    {
        PlayGamesPlatform.Instance.Authenticate((GooglePlayGames.BasicApi.SignInStatus status) => {
            if (status == GooglePlayGames.BasicApi.SignInStatus.Success)
            {
                Log("Google Play Games 로그인 성공");

                try
                {
                    var playerName = PlayGamesPlatform.Instance.GetUserDisplayName();
                    var playerId = PlayGamesPlatform.Instance.GetUserId();
                    Log($"플레이어 정보: {playerName} ({playerId})");

                    try
                    {
                        PlayGamesPlatform.Instance.RequestServerSideAccess(true, (string authCode) => {
                            if (!string.IsNullOrEmpty(authCode))
                            {
                                Credential credential = PlayGamesAuthProvider.GetCredential(authCode);
                                SignInWithFirebaseCredential(credential);
                            }
                            else
                            {
                                Log("서버 인증 코드를 획득할 수 없습니다. 일반 Google 로그인으로 전환합니다.");
                                SignInWithGoogle();
                            }
                        });
                    }
                    catch (Exception)
                    {
                        Log("Play Games 인증 정보를 Firebase에 연결할 수 없습니다. 일반 Google 로그인으로 전환합니다.");
                        SignInWithGoogle();
                    }
                }
                catch (Exception e)
                {
                    Log("플레이어 정보를 가져올 수 없습니다: " + e.Message);
                    SignInWithGoogle();
                }
            }
            else
            {
                Log($"Google Play Games 로그인 실패: {status}");
            }
        });
    }

    private void SignInWithFirebaseCredential(Credential credential)
    {
        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task => {
            if (task.IsCanceled)
            {
                Log("Firebase 인증 취소됨");
                return;
            }
            if (task.IsFaulted)
            {
                Log($"Firebase 인증 실패: {task.Exception}");
                return;
            }

            FirebaseUser user = task.Result;
            Log($"Firebase 인증 성공: {user.DisplayName} ({user.UserId})");
        });
    }

    private void InitializeAdMob()
    {
        MobileAds.Initialize(initStatus => {
            Log("AdMob 초기화 성공");

            // 광고 초기화 후 배너 광고 로드
            RequestBannerAd();
            RequestInterstitialAd();
        });
    }

    private void RequestBannerAd()
    {
        // 이전 배너 파괴
        if (bannerView != null)
        {
            bannerView.Destroy();
        }

        // 새 배너 생성
        bannerView = new BannerView(bannerAdUnitId, AdSize.Banner, AdPosition.Bottom);

        // 이벤트 핸들러
        bannerView.OnBannerAdLoaded += () => {
            Log("배너 광고 로드 성공");
        };

        bannerView.OnBannerAdLoadFailed += (LoadAdError error) => {
            Log($"배너 광고 로드 실패: {error.GetMessage()}");
        };

        // 새로운 방식의 광고 요청 생성
        AdRequest request = new AdRequest();

        // 배너 광고 로드
        bannerView.LoadAd(request);
    }

    private void RequestInterstitialAd()
    {
        // 광고가 이미 로드되어 있는 경우 제거
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        Log("전면 광고 로드 시작...");

        AdRequest request = new AdRequest();
        InterstitialAd.Load(interstitialAdUnitId, request, OnInterstitialAdLoaded);
    }

    private void OnInterstitialAdLoaded(GoogleMobileAds.Api.InterstitialAd ad, LoadAdError error)
    {
        if (error != null)
        {
            Log($"전면 광고 로드 실패: {error.GetMessage()}");
            return;
        }

        interstitialAd = ad;
        Log("전면 광고 로드 성공");

        interstitialAd.OnAdFullScreenContentClosed += () => {
            Log("전면 광고가 닫혔습니다.");
            RequestInterstitialAd();
        };

        interstitialAd.OnAdFullScreenContentFailed += (AdError adError) => {
            Log($"전면 광고 표시 실패: {adError.GetMessage()}");
            RequestInterstitialAd();
        };
    }

    public void ShowInterstitialAd()
    {
        if (interstitialAd != null)
        {
            interstitialAd.Show();
        }
        else
        {
            Log("전면 광고가 아직 로드되지 않았습니다.");
            RequestInterstitialAd();
        }
    }

    public void HideBannerAd()
    {
        if (bannerView != null)
        {
            bannerView.Hide();
        }
    }

    public void ShowBannerAd()
    {
        if (bannerView != null)
        {
            bannerView.Show();
        }
        else
        {
            RequestBannerAd();
        }
    }

    public void UnlockAchievement()
    {
        if (PlayGamesPlatform.Instance.IsAuthenticated())
        {
            PlayGamesPlatform.Instance.UnlockAchievement(achievementId, (bool success) => {
                if (success)
                {
                    Log("업적 달성 성공");
                }
                else
                {
                    Log("업적 달성 실패");
                }
            });
        }
        else
        {
            Log("업적 달성 전 로그인이 필요합니다.");
            SignInWithGooglePlayGames();
        }
    }

    public void IncrementAchievement(int steps = 1)
    {
        if (PlayGamesPlatform.Instance.IsAuthenticated())
        {
            PlayGamesPlatform.Instance.IncrementAchievement(achievementId, steps, (bool success) => {
                if (success)
                {
                    Log($"업적 진행도 증가 성공: {steps}단계");
                }
                else
                {
                    Log("업적 진행도 증가 실패");
                }
            });
        }
        else
        {
            Log("업적 진행 전 로그인이 필요합니다.");
            SignInWithGooglePlayGames();
        }
    }

    public void PostScoreToLeaderboard(long score)
    {
        if (PlayGamesPlatform.Instance.IsAuthenticated())
        {
            PlayGamesPlatform.Instance.ReportScore(score, leaderboardId, (bool success) => {
                if (success)
                {
                    Log($"리더보드 점수 등록 성공: {score}점");
                }
                else
                {
                    Log("리더보드 점수 등록 실패");
                }
            });
        }
        else
        {
            Log("점수 등록 전 로그인이 필요합니다.");
            SignInWithGooglePlayGames();
        }
    }

    public void ShowAchievementsUI()
    {
        if (PlayGamesPlatform.Instance.IsAuthenticated())
        {
            Social.ShowAchievementsUI();
        }
        else
        {
            Log("업적 UI 표시 전 로그인이 필요합니다.");
            SignInWithGooglePlayGames();
        }
    }

    public void ShowLeaderboardUI()
    {
        if (PlayGamesPlatform.Instance.IsAuthenticated())
        {
            Social.ShowLeaderboardUI();
        }
        else
        {
            Log("리더보드 UI 표시 전 로그인이 필요합니다.");
            SignInWithGooglePlayGames();
        }
    }

    private void OnDestroy()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
        }

        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
        }
    }

    public void AddScore()
    {
        PostScoreToLeaderboard(UnityEngine.Random.Range(100, 1000));
    }
}