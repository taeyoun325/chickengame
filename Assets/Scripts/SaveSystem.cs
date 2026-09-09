using System;
using System.IO;
using UnityEngine;

/// <summary>디스크에 저장되는 진행 상황.</summary>
[Serializable]
public sealed class SaveData
{
    public int day = 1;
    public int revenue;
    public int totalOrders;
    public int successfulOrders;
    public int failedOrders;
    public int burntChicken;
    public int wastedFood;
    public int spending;
    public int reputation = 100;
    public int[] upgradeLevels = Array.Empty<int>();
    public string savedAt = string.Empty;
}

public static class SaveSystem
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "chickengame-save.json");

    public static bool HasSave => File.Exists(FilePath);

    public static void Save(SaveData data)
    {
        try
        {
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"세이브 실패: {exception.Message}");
        }
    }

    public static SaveData Load()
    {
        if (!HasSave)
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"세이브 불러오기 실패: {exception.Message}");
            return null;
        }
    }

    public static void Delete()
    {
        if (HasSave)
        {
            File.Delete(FilePath);
        }
    }
}
