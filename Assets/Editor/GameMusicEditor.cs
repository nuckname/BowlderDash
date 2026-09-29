using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameMusic))]
public sealed class GameMusicEditor : Editor
{
    private float fadeOverride = -1f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play Mode Music Preview", EditorStyles.boldLabel);
        if (!Application.isPlaying || GameAudio.Instance == null || GameAudio.Instance.Music != target)
        {
            EditorGUILayout.HelpBox("Select the active GameAudio instance in Play Mode to preview music.", MessageType.Info);
            return;
        }

        var music = (GameMusic)target;
        EditorGUI.BeginChangeCheck();
        float volume = EditorGUILayout.Slider("Shared Music Volume", GameAudio.Instance.MusicVolume, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) GameAudio.Instance.SetMusicVolume(volume);
        fadeOverride = EditorGUILayout.FloatField("Fade Override (-1 = Profile)", fadeOverride);
        foreach (MusicPhase phase in System.Enum.GetValues(typeof(MusicPhase)))
        {
            if (GUILayout.Button($"Request {phase}")) music.RequestPhase(phase, fadeOverride);
            EditorGUILayout.LabelField($"{phase} Fade Gain", music.GetFadeGain(phase).ToString("F3"));
            EditorGUILayout.LabelField($"{phase} Playback",
                $"{(music.IsPlaying(phase) ? "Running" : "Stopped")} at {music.GetPlaybackSeconds(phase):F2}s");
        }
        EditorGUILayout.LabelField("Gameplay/Shop Pair", music.GameplayShopPlaybackState);
        EditorGUILayout.LabelField("Target", music.TargetPhase?.ToString() ?? "None");
        EditorGUILayout.LabelField("Pending", music.PendingPhase?.ToString() ?? "None");
        Repaint();
    }
}
