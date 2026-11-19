using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class TutorialModalController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Tutorial Slides (PNGs)")]
    [SerializeField] private List<Texture2D> tutorialSlides = new List<Texture2D>();

    private VisualElement _overlay;
    private VisualElement _imageContainer;
    private Button _closeXButton;
    private Button _backButton;
    private Button _nextButton;
    private Button _confirmButton;
    private Label _pageCounterLabel;

    private int _currentIndex = 0;

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        var root = uiDocument.rootVisualElement;

        _overlay = root.Q<VisualElement>("tutorial-overlay");
        _imageContainer = root.Q<VisualElement>("image-container");
        _closeXButton = root.Q<Button>("btn-close-x");
        _backButton = root.Q<Button>("btn-back");
        _nextButton = root.Q<Button>("btn-next");
        _confirmButton = root.Q<Button>("btn-confirm");
        _pageCounterLabel = root.Q<Label>("page-counter");

        _closeXButton.clicked += CloseModal;
        _backButton.clicked += OnBackClicked;
        _nextButton.clicked += OnNextClicked;
        _confirmButton.clicked += CloseModal;

        // Show automatically when hitting Play
        OpenModal();
    }

    private void OnDisable()
    {
        if (_closeXButton != null) _closeXButton.clicked -= CloseModal;
        if (_backButton != null) _backButton.clicked -= OnBackClicked;
        if (_nextButton != null) _nextButton.clicked -= OnNextClicked;
        if (_confirmButton != null) _confirmButton.clicked -= CloseModal;
    }

    public void OpenModal()
    {
        if (tutorialSlides == null || tutorialSlides.Count == 0)
        {
            Debug.LogWarning("TutorialModalController: No tutorial slides assigned!");
            CloseModal();
            return;
        }

        _currentIndex = 0;
        _overlay.style.display = DisplayStyle.Flex;
        UpdateUI();
    }

    private void OnBackClicked()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            UpdateUI();
        }
    }

    private void OnNextClicked()
    {
        if (_currentIndex < tutorialSlides.Count - 1)
        {
            _currentIndex++;
            UpdateUI();
        }
    }

    private void CloseModal()
    {
        _overlay.style.display = DisplayStyle.None;
    }

    private void UpdateUI()
    {
        // Update image
        Texture2D currentSlide = tutorialSlides[_currentIndex];
        _imageContainer.style.backgroundImage = new StyleBackground(currentSlide);

        // Update counter (e.g., "1 / 4")
        _pageCounterLabel.text = $"{_currentIndex + 1} / {tutorialSlides.Count}";

        // Update button enabled states
        bool isFirstSlide = _currentIndex == 0;
        bool isLastSlide = _currentIndex == tutorialSlides.Count - 1;

        _backButton.SetEnabled(!isFirstSlide);
        _nextButton.SetEnabled(!isLastSlide);
        _confirmButton.SetEnabled(isLastSlide);
    }
}