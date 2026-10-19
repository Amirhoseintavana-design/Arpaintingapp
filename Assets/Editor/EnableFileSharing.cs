#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Editor-only: runs automatically after every iOS build.
// Turns on "file sharing" so the CSV files from DataLogger appear in the
// iPhone's Files app (On My iPhone → app name) and in Finder on the Mac.
// Put this file in:  Assets/Editor/EnableFileSharing.cs
public static class EnableFileSharing
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(buildPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("UIFileSharingEnabled", true);            // visible in Finder / Files
        plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true); // visible in the Files app
        plist.WriteToFile(plistPath);
    }
}
#endif