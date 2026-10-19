using System;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance;

    [Header("UI")]
    [SerializeField] private TMP_InputField joinInput;
    [SerializeField] private TMP_InputField nameInput; // Ново поле за въвеждане на име
    [SerializeField] private TMP_Text statusText;

    [Header("Elements to Hide when game starts")]
    [SerializeField] private GameObject createButton;
    [SerializeField] private GameObject joinButton;
    [SerializeField] private GameObject joinInputObject;
    [SerializeField] private GameObject nameInputObject; // Обектът на полето за име, за да го скрием след вход

    private ISession currentSession;
    public string PlayerName { get; private set; } = "Player";

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            Debug.Log("Unity Services initialized.");
            Debug.Log("Player ID: " + AuthenticationService.Instance.PlayerId);

            // Зареждаме запазено име, ако има такова
            if (PlayerPrefs.HasKey("PlayerName") && nameInput != null)
            {
                nameInput.text = PlayerPrefs.GetString("PlayerName");
            }

            SetStatus("Ready");
        }
        catch (Exception e)
        {
            Debug.LogError("Unity Services initialization failed: " + e);
            SetStatus("Initialization failed");
        }
    }

    private bool ValidateAndSaveName()
    {
        if (nameInput != null)
        {
            string enteredName = nameInput.text.Trim();
            if (string.IsNullOrEmpty(enteredName))
            {
                SetStatus("Моля въведете име преди да продължите!");
                return false;
            }
            PlayerName = enteredName;
            PlayerPrefs.SetString("PlayerName", PlayerName);
            PlayerPrefs.Save();
        }
        return true;
    }

    public async void CreateGame()
    {
        if (!ValidateAndSaveName()) return;

        SetStatus("Creating game...");

        try
        {
            var options = new SessionOptions
            {
                MaxPlayers = 4,
                // Можем да добавим името на хоста към данните на сесията, ако е нужно
                Name = PlayerName + "'s Game"
            }.WithRelayNetwork();

            currentSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            Debug.Log("GAME CREATED!");
            Debug.Log("Join Code: " + currentSession.Code);

            // Преместваме текстовото поле в горния десен ъгъл
            if (statusText != null)
            {
                RectTransform rectTransform = statusText.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchorMin = new Vector2(1, 1);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.pivot = new Vector2(1, 1);
                    rectTransform.anchoredPosition = new Vector2(-30, -30);
                }
            }

            SetStatus("Join Code: " + currentSession.Code);

            // Скриваме бутоните и полетата
            HideGameCreationElements();

            currentSession.Changed += OnSessionChanged;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to create game: " + e);
            SetStatus("Failed to create game");
        }
    }

    public async void JoinGame()
    {
        if (!ValidateAndSaveName()) return;

        string joinCode = joinInput.text.Trim();

        if (string.IsNullOrEmpty(joinCode))
        {
            SetStatus("Enter a join code first.");
            return;
        }

        SetStatus("Joining...");

        try
        {
            currentSession =
                await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);

            Debug.Log("JOINED GAME!");
            Debug.Log("Session ID: " + currentSession.Id);

            SetStatus("Joined game!");

            // Скриваме бутоните и полетата
            HideGameCreationElements();
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to join game: " + e);
            SetStatus("Failed to join game");
        }
    }

    private void OnSessionChanged()
    {
        if (currentSession == null)
            return;

        Debug.Log("Players in session: " + currentSession.Players.Count);
    }

    private void HideGameCreationElements()
    {
        if (createButton != null) createButton.SetActive(false);
        if (joinButton != null) joinButton.SetActive(false);
        if (joinInputObject != null) joinInputObject.SetActive(false);
        if (nameInputObject != null) nameInputObject.SetActive(false);
    }

    private void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void OnDestroy()
    {
        if (currentSession != null)
        {
            currentSession.Changed -= OnSessionChanged;
        }
    }
}