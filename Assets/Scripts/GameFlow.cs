using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameState
{
    Title,
    Playing,
    Paused,
    Settlement,
    Victory,
    Defeat
}

/// <summary>타이틀 → 영업 → 결산 → 승패로 이어지는 화면 흐름을 관리한다.</summary>
public sealed class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    private GameState state = GameState.Title;
    private RestaurantGame game;
    private GameObject titlePanel;
    private GameObject pausePanel;
    private GameObject settlementPanel;
    private GameObject resultPanel;
    private Text titleText;
    private Text settlementText;
    private Text resultText;
    private GameObject localPlayerRoot;
    private float autoAdvanceTimer;

    private static bool AutoAdvanceDays =>
        System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoday") >= 0;

    public GameState State => state;
    public bool IsPlaying => state == GameState.Playing;

    private void Awake()
    {
        Instance = this;
    }

    public void Initialise(RestaurantGame restaurantGame, GameObject localPlayers)
    {
        game = restaurantGame;
        localPlayerRoot = localPlayers;
    }

    /// <summary>-mode host / -mode client -address 192.168.0.5 처럼 실행 인자로 바로 시작할 수 있다.
    /// LAN 파티에서 타이틀을 거치지 않고 켜거나, 자동 테스트를 돌릴 때 쓴다.</summary>
    private bool TryStartFromCommandLine()
    {
        string mode = null;
        string address = "127.0.0.1";
        string[] args = System.Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "-mode")
            {
                mode = args[index + 1].ToLowerInvariant();
            }
            else if (args[index] == "-address")
            {
                address = args[index + 1];
            }
        }

        switch (mode)
        {
            case "host":
                StartNetworkGame(true, address);
                return true;
            case "client":
                StartNetworkGame(false, address);
                return true;
            case "local":
                StartGame(false);
                return true;
            default:
                return false;
        }
    }

    public void ConnectPanels(GameObject title, Text titleLabel, GameObject pause, GameObject settlement, Text settlementLabel, GameObject result, Text resultLabel)
    {
        titlePanel = title;
        titleText = titleLabel;
        pausePanel = pause;
        settlementPanel = settlement;
        settlementText = settlementLabel;
        resultPanel = result;
        resultText = resultLabel;
        EnterTitle();
        TryStartFromCommandLine();
    }

    private void Update()
    {
        // -autoday: 무인 장시간 테스트에서 결산 화면을 자동으로 넘긴다.
        if (state == GameState.Settlement && AutoAdvanceDays)
        {
            autoAdvanceTimer -= Time.unscaledDeltaTime;
            if (autoAdvanceTimer <= 0f && game != null && KitchenNetwork.IsHostSide)
            {
                game.StartNextDay();
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        switch (state)
        {
            case GameState.Title:
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    StartGame(false);
                }
                else if (keyboard.cKey.wasPressedThisFrame && SaveSystem.HasSave)
                {
                    StartGame(true);
                }
                else if (keyboard.hKey.wasPressedThisFrame)
                {
                    StartNetworkGame(host: true);
                }
                else if (keyboard.jKey.wasPressedThisFrame)
                {
                    StartNetworkGame(host: false);
                }

                break;
            case GameState.Playing:
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Pause();
                }

                break;
            case GameState.Paused:
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Resume();
                }
                else if (keyboard.rKey.wasPressedThisFrame)
                {
                    Restart();
                }

                break;
            case GameState.Settlement:
                // 접속한 플레이어는 호스트가 다음 DAY 를 시작할 때까지 기다린다.
                if (keyboard.spaceKey.wasPressedThisFrame && game != null && KitchenNetwork.IsHostSide)
                {
                    game.StartNextDay();
                }

                break;
            case GameState.Victory:
            case GameState.Defeat:
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    Restart();
                }

                break;
        }
    }

    public void EnterTitle()
    {
        state = GameState.Title;
        Time.timeScale = 0f;
        SetPanels(title: true);
        if (titleText != null)
        {
            titleText.text = BuildTitleText();
        }
    }

    private void StartGame(bool continueSave)
    {
        if (continueSave && game != null)
        {
            game.LoadProgress();
        }
        else
        {
            SaveSystem.Delete();
        }

        state = GameState.Playing;
        Time.timeScale = 1f;
        SetPanels();
    }

    /// <summary>호스트로 열거나 같은 네트워크의 호스트에 접속한다.</summary>
    private void StartNetworkGame(bool host, string address = "127.0.0.1")
    {
        if (NetworkSession.Instance == null)
        {
            return;
        }

        SaveSystem.Delete();
        bool started = host
            ? NetworkSession.Instance.StartHost()
            : NetworkSession.Instance.StartClient(address);

        if (!started)
        {
            return;
        }

        // 네트워크 모드에서는 각자 자기 캐릭터를 스폰하므로 로컬 2인용 캐릭터는 접는다.
        if (localPlayerRoot != null)
        {
            localPlayerRoot.SetActive(false);
        }

        state = GameState.Playing;
        Time.timeScale = 1f;
        SetPanels();
    }

    public void Pause()
    {
        state = GameState.Paused;
        Time.timeScale = 0f;
        SetPanels(pause: true);
    }

    public void Resume()
    {
        state = GameState.Playing;
        Time.timeScale = 1f;
        SetPanels();
    }

    public void EnterSettlement(string report)
    {
        Debug.Log("[Day] 결산 화면");
        autoAdvanceTimer = 3f;
        state = GameState.Settlement;
        Time.timeScale = 0f;
        if (settlementText != null)
        {
            settlementText.text = report;
        }

        SetPanels(settlement: true);
    }

    public void ResumeFromSettlement()
    {
        state = GameState.Playing;
        Time.timeScale = 1f;
        SetPanels();
    }

    public void EnterVictory(string report)
    {
        state = GameState.Victory;
        Time.timeScale = 0f;
        ShowResult(report);
    }

    public void EnterDefeat(string report)
    {
        state = GameState.Defeat;
        Time.timeScale = 0f;
        ShowResult(report);
    }

    private void ShowResult(string report)
    {
        if (resultText != null)
        {
            resultText.text = report;
        }

        SetPanels(result: true);
    }

    private void Restart()
    {
        Time.timeScale = 1f;

        // 네트워크 세션을 정리하지 않고 씬을 다시 불러오면 NetworkManager 가 중복된다.
        if (NetworkSession.Instance != null)
        {
            NetworkSession.Instance.Shutdown();
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void SetPanels(bool title = false, bool pause = false, bool settlement = false, bool result = false)
    {
        if (titlePanel != null) titlePanel.SetActive(title);
        if (pausePanel != null) pausePanel.SetActive(pause);
        if (settlementPanel != null) settlementPanel.SetActive(settlement);
        if (resultPanel != null) resultPanel.SetActive(result);
    }

    private static string BuildTitleText()
    {
        string continueLine = SaveSystem.HasSave ? "C  이어하기" : "저장된 기록 없음";
        string recordLine = BuildRecordLine();
        return "CHICKEN GAME\n\n" +
               "누적 매출 ₩10,000,000 을 목표로\n치킨집을 운영하세요\n\n" +
               "SPACE  로컬 2인 새 게임\n" + continueLine + "\n" +
               "H  호스트로 열기      J  호스트에 접속\n\n" +
               "P1 WASD+E   P2 IJKL+O   게임패드 자동 인식\nESC 일시정지" + recordLine;
    }

    /// <summary>이전 기록이 있으면 타이틀에 함께 보여준다.</summary>
    private static string BuildRecordLine()
    {
        SaveData data = SaveSystem.Load();
        if (data == null || data.bestDayRevenue <= 0)
        {
            return string.Empty;
        }

        return $"\n\n최고 기록  DAY {data.bestDay}   하루 매출 ₩{data.bestDayRevenue:N0}";
    }
}
