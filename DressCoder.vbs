Option Explicit

Dim shell, files, projectRoot, scriptPath
Set shell = CreateObject("WScript.Shell")
Set files = CreateObject("Scripting.FileSystemObject")

projectRoot = files.GetParentFolderName(WScript.ScriptFullName)
scriptPath = files.BuildPath(projectRoot, "src\frontend.py")

If Not files.FileExists(scriptPath) Then
    MsgBox "Could not find DressCoder at:" & vbCrLf & scriptPath, _
        vbCritical, "DressCoder"
    WScript.Quit 1
End If

shell.CurrentDirectory = projectRoot
shell.Run "pythonw.exe """ & scriptPath & """", 0, False
