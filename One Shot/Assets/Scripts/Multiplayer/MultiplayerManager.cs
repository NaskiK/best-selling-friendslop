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
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject multiplayerUI;

    private ISession currentSession;

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

            SetStatus("Ready");
        }
        catch (Exception e)
        {
            Debug.LogError("Unity Services initialization failed: " + e);
            SetStatus("Initialization failed");
        }
    }

    public async void CreateGame()
    {
        SetStatus("Creating game...");

        try
        {
            var options = new SessionOptions
            {
                MaxPlayers = 4
            }.WithRelayNetwork();

            currentSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            Debug.Log("GAME CREATED!");
            Debug.Log("Join Code: " + currentSession.Code);

            // Keep the UI visible so Player 1 can see the code.
            SetStatus("Join Code: " + currentSession.Code);

            // Watch for other players joining.
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

            // Player 2 can hide their UI immediately.
            HideMultiplayerUI();
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

        Debug.Log(
            "Players in session: " +
            currentSession.Players.Count
        );

        // Once at least 2 players are in the session,
        // hide Player 1's UI too.
        if (currentSession.Players.Count >= 2)
        {
            HideMultiplayerUI();
        }
    }

    private void HideMultiplayerUI()
    {
        if (multiplayerUI != null)
        {
            multiplayerUI.SetActive(false);
        }
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