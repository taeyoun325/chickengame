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

    public GameState State => state;
    public bool IsPlaying => state == GameState.Playing;

    private void Awake()
    {
        Instance = this;
    }

    public void Initialise(RestaurantGame restaurantGame)
    {
        game = restaurantGame;
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
    }

    private void Update()
    {
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
                if (keyboard.spaceKey.wasPressedThisFrame && game != null)
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
        return "CHICKEN GAME\n\n" +
               "누적 매출 ₩10,000,000 을 목표로\n치킨집을 운영하세요\n\n" +
               "SPACE  새 게임\n" + continueLine + "\n\n" +
               "P1 WASD + E     P2 IJKL + E\nESC 일시정지";
    }
}
