Add-Type -AssemblyName System.Drawing
Add-Type @"
using System.Runtime.InteropServices;
public class D { [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(int v); }
"@
[D]::SetProcessDpiAwarenessContext(-4) | Out-Null
$b = New-Object System.Drawing.Bitmap 1100,3840
$g = [System.Drawing.Graphics]::FromImage($b)
$g.CopyFromScreen(3840,-3573,0,0,$b.Size)
$b.Save($args[0], [System.Drawing.Imaging.ImageFormat]::Png)
