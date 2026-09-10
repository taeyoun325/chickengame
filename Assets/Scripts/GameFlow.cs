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
    private bool typingAddress;
    private string typedAddress = string.Empty;

    private const string LastAddressKey = "chickengame.lastAddress";

    // 밸런스 측정도 하루가 자동으로 넘어가야 진행된다.
    private static bool AutoAdvanceDays =>
        System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoday") >= 0 || BalanceTest.Requested;

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

        if (typingAddress)
        {
            UpdateAddressEntry(keyboard);
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
                    BeginAddressEntry();
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
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    Restart();
                }

                break;
            case GameState.Defeat:
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    Restart();
                }
                else if (keyboard.rKey.wasPressedThisFrame && game != null && KitchenNetwork.IsHostSide)
                {
                    game.Reopen();
                }

                break;
        }
    }

    /// <summary>친구 PC 에 붙으려면 주소를 칠 수 있어야 한다.
    /// 지난번 주소를 기억해 두어 다음 판은 Enter 한 번이면 된다.</summary>
    private void BeginAddressEntry()
    {
        typingAddress = true;
        typedAddress = PlayerPrefs.GetString(LastAddressKey, "127.0.0.1");
        RefreshTitleText();
    }

    private void UpdateAddressEntry(Keyboard keyboard)
    {
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            typingAddress = false;
            RefreshTitleText();
            return;
        }

        if (keyboard.backspaceKey.wasPressedThisFrame && typedAddress.Length > 0)
        {
            typedAddress = typedAddress.Substring(0, typedAddress.Length - 1);
            RefreshTitleText();
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            string address = string.IsNullOrWhiteSpace(typedAddress) ? "127.0.0.1" : typedAddress.Trim();
            PlayerPrefs.SetString(LastAddressKey, address);
            PlayerPrefs.Save();
            typingAddress = false;
            StartNetworkGame(false, address);
            return;
        }

        AppendTypedCharacters(keyboard);
    }

    /// <summary>주소에 쓰이는 숫자와 점만 받는다.</summary>
    private void AppendTypedCharacters(Keyboard keyboard)
    {
        if (typedAddress.Length >= 21)
        {
            return;
        }

        for (int digit = 0; digit <= 9; digit++)
        {
            Key key = Key.Digit0 + digit;
            Key numpad = Key.Numpad0 + digit;
            if (keyboard[key].wasPressedThisFrame || keyboard[numpad].wasPressedThisFrame)
            {
                typedAddress += (char)('0' + digit);
                RefreshTitleText();
                return;
            }
        }

        if (keyboard[Key.Period].wasPressedThisFrame || keyboard[Key.NumpadPeriod].wasPressedThisFrame)
        {
            typedAddress += '.';
            RefreshTitleText();
        }
    }

    private void RefreshTitleText()
    {
        if (titleText == null)
        {
            return;
        }

        titleText.text = typingAddress
            ? "호스트 주소를 입력하세요\n\n" + typedAddress + "_\n\n" +
              "숫자와 . 만 입력됩니다\nENTER 접속      BACKSPACE 지우기      ESC 취소"
            : BuildTitleText();
    }

    public void EnterTitle()
    {
        state = GameState.Title;
        Time.timeScale = 0f;
        typingAddress = false;
        SetPanels(title: true);
        RefreshTitleText();
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
        GameSpeed.Resume();
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

        // 네트워크 모드에서는 각자 자기 캐릭터를 스폰하므로 로컬 캐릭터는 접는다.
        if (localPlayerRoot != null)
        {
            localPlayerRoot.SetActive(false);
        }

        state = GameState.Playing;
        GameSpeed.Resume();
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
        GameSpeed.Resume();
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
        GameSpeed.Resume();
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
        GameSpeed.Resume();

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
               "SPACE  혼자 시작\n" + continueLine + "\n" +
               "H  호스트로 열기      J  친구에게 접속  (최대 4인)\n\n" +
               "WASD 이동   마우스 시점   SPACE 점프   E 상호작용\nF 내려놓기   Q 소스   ESC 일시정지" + recordLine;
    }

    /// <summary>이전 기록이 있으면 타이틀에 함께 보여준다.</summary>
    private static string BuildRecordLine()
    {
        SaveData data = SaveSystem.Load();
        if (data == null)
        {
            return string.Empty;
        }

        // 이 게임의 기록은 달성 시간이다. 그것부터 보여준다.
        string line = string.Empty;
        if (data.bestClearSeconds > 0)
        {
            line += $"\n\n최고 기록  ₩10,000,000 달성 {RestaurantGame.Clock(data.bestClearSeconds)}";
        }

        if (data.bestDayRevenue > 0)
        {
            line += $"\n최고 DAY {data.bestDay}   하루 매출 ₩{data.bestDayRevenue:N0}";
        }

        return line;
    }
}
