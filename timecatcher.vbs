Set objShell = CreateObject("WScript.Shell")

' Replace "path_to_your_exe" with the actual path to your executable
exePath = "timecatcher.exe"

' Run the executable
objShell.Run """" & exePath & """", 1, True