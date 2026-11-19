using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class GameOverModalController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public event Action OnPlayAgainRequested;
    public event Action OnMainMenuRequested;

    private Label _outcomeTitleLabel;
    private Label _roundsSurvivedLabel;
    private Button _mainMenuButton;
    private Button _playAgainButton;

    private bool _gameEnded;
    private int _currentDay;

    private static readonly Color WinColor = new Color(0.43f, 0.86f, 0.51f);
    private static readonly Color LoseColor = new Color(0.95f, 0.45f, 0.45f);
    private int _roundsStarted;

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        var root = uiDocument.rootVisualElement;

        _outcomeTitleLabel = root.Q<Label>("OutcomeTitleLabel");
        _roundsSurvivedLabel = root.Q<Label>("RoundsSurvivedLabel");
        _mainMenuButton = root.Q<Button>("MainMenuButton");
        _playAgainButton = root.Q<Button>("PlayAgainButton");

        if (_mainMenuButton != null) _mainMenuButton.clicked += HandleMainMenuClicked;
        if (_playAgainButton != null) _playAgainButton.clicked += HandlePlayAgainClicked;

        UFO.OnFarmerAbducted += HandleFarmerAbducted;
        WaveManager.OnWaveStarted += HandleWaveStarted;
        WaveManager.OnAllWavesCleared += HandleAllWavesCleared;

        _gameEnded = false;
        _roundsStarted = 0;

        root.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        if (_mainMenuButton != null) _mainMenuButton.clicked -= HandleMainMenuClicked;
        if (_playAgainButton != null) _playAgainButton.clicked -= HandlePlayAgainClicked;

        UFO.OnFarmerAbducted -= HandleFarmerAbducted;
        WaveManager.OnWaveStarted -= HandleWaveStarted;
        WaveManager.OnAllWavesCleared -= HandleAllWavesCleared;
    }

    private void HandleWaveStarted(int roundNumber)
    {
        _roundsStarted = roundNumber;
    }

    private void HandleAllWavesCleared(int totalRoundsSurvived)
    {
        ShowGameOver(isVictory: true, roundsSurvived: totalRoundsSurvived);
    }

    private void HandleFarmerAbducted()
    {
        // If lost during Round 1, 0 rounds were survived; during Round 3, 2 rounds were survived
        int roundsSurvived = Mathf.Max(0, _roundsStarted - 1);
        ShowGameOver(isVictory: false, roundsSurvived: roundsSurvived);
    }

    public void ShowGameOver(bool isVictory, int roundsSurvived)
    {
        if (_gameEnded) return;
        _gameEnded = true;

        _outcomeTitleLabel.text = isVictory ? "You Win!" : "You Lose!";
        _outcomeTitleLabel.style.color = isVictory ? WinColor : LoseColor;
        _roundsSurvivedLabel.text = $"Rounds Survived: {roundsSurvived}";

        Time.timeScale = 0f;
        uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
    }

    private void HandlePlayAgainClicked()
    {
        Time.timeScale = 1f;
        OnPlayAgainRequested?.Invoke();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void HandleMainMenuClicked()
    {
        Time.timeScale = 1f;
        OnMainMenuRequested?.Invoke();

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }
}