using UnityEngine;
using Unity.Netcode;

public class TavernMusicDistance : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource music;

    [Header("Distance")]
    public float fullVolumeDistance = 5f;
    public float silentDistance = 30f;

    [Header("Fade")]
    public float fadeSpeed = 2f;

    private Transform localPlayer;

    private void Start()
    {
        if (music == null)
            music = GetComponent<AudioSource>();

        if (music == null)
        {
            Debug.LogError("TavernMusic: No AudioSource found!");
            enabled = false;
            return;
        }

        // Make the music unaffected by camera rotation.
        music.spatialBlend = 0f;
        music.loop = true;

        if (!music.isPlaying)
            music.Play();
    }

    private void Update()
    {
        // Find the local player if we haven't found one yet.
        if (localPlayer == null)
            FindLocalPlayer();

        if (localPlayer == null)
            return;

        // Measure distance between the player and the music source.
        float distance = Vector3.Distance(
            localPlayer.position,
            transform.position
        );

        // Full volume nearby, fading to silence farther away.
        float targetVolume = 1f - Mathf.InverseLerp(
            fullVolumeDistance,
            silentDistance,
            distance
        );

        targetVolume = Mathf.Clamp01(targetVolume);

        // Smoothly change the volume.
        music.volume = Mathf.MoveTowards(
            music.volume,
            targetVolume,
            fadeSpeed * Time.deltaTime
        );

        Debug.Log(
            "Music distance: " + distance.ToString("F1") +
            " | Target volume: " + targetVolume.ToString("F2") +
            " | Current volume: " + music.volume.ToString("F2")
        );
    }

    private void FindLocalPlayer()
    {
        NetworkObject[] players =
            FindObjectsByType<NetworkObject>(
                FindObjectsSortMode.None
            );

        foreach (NetworkObject player in players)
        {
            if (player.IsOwner && player.CompareTag("Player"))
            {
                localPlayer = player.transform;
                Debug.Log("TavernMusic: Found local player!");
                return;
            }
        }
    }
}