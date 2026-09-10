using System;
using UnityEngine;

/// <summary>-verbose 로 실행했을 때만 남는 진단 로그. 검증용이라 평소에는 조용하다.</summary>
public static class GameLog
{
    private static bool? verbose;

    public static bool IsVerbose
    {
        get
        {
            if (verbose.HasValue)
            {
                return verbose.Value;
            }

            verbose = Array.IndexOf(Environment.GetCommandLineArgs(), "-verbose") >= 0;
            return verbose.Value;
        }
    }

    public static void Verbose(string message)
    {
        if (IsVerbose)
        {
            Debug.Log(message);
        }
    }
}
