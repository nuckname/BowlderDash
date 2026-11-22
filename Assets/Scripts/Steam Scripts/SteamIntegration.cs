using UnityEngine;

public class SteamIntegration : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        try
        {
           Steamworks.SteamClient.Init(5607750);
           print(Steamworks.SteamClient.Name);

           UnlockTestAchievement("Test_Ach");
        }
        catch (System.Exception e)
        {
            Debug.Log(e);
        }
    }
    
    void Update()
    {
        Steamworks.SteamClient.RunCallbacks();
    }

    void OnApplicationQuit()
    {
        Steamworks.SteamClient.Shutdown();
    }
    
    public void UnlockTestAchievement(string APIname)
    {
        if (!Steamworks.SteamClient.IsValid)
        {
            return;
        }

        var achievement = new Steamworks.Data.Achievement(APIname);
        achievement.Trigger();
    }
}
