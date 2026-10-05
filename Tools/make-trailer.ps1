# Turns the frames recorded by the TrailerCapture PlayMode test into the trailer MP4 and the README GIF.
#   powershell -ExecutionPolicy Bypass -File Tools/run-unity-tests.ps1 -TestPlatform PlayMode -Graphics -TestFilter CadenceClub.PlayModeTests.TrailerCapture
#   powershell -ExecutionPolicy Bypass -File Tools/make-trailer.ps1
# Needs ffmpeg on PATH (winget install Gyan.FFmpeg).
param(
    [int]$GifWidth = 270,
    [int]$GifFps = 15,
    [int]$GifSeconds = 12  # the gameplay part: combo, victory lap, rider powers
)

$ErrorActionPreference = "Stop"
$project = Resolve-Path (Join-Path $PSScriptRoot "..")
$frames = Join-Path $project "Logs/frames/frame_%04d.tga"
$gif = Join-Path $project "docs/cadence-club.gif"
$mp4 = Join-Path $project "docs/cadence-club-trailer.mp4"

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { throw "ffmpeg not found. Install it with: winget install Gyan.FFmpeg" }
if (-not (Test-Path (Join-Path $project "Logs/frames/frame_0000.tga"))) { throw "No frames in Logs/frames. Run the TrailerCapture test first (see the header of this script)." }
New-Item -ItemType Directory -Force (Split-Path $gif) | Out-Null

& ffmpeg -hide_banner -loglevel error -y -framerate 30 -i $frames -c:v libx264 -pix_fmt yuv420p -crf 18 -movflags +faststart $mp4
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed making the MP4" }

# Two-pass palette keeps the colours clean; diff_mode re-dithers only what moves.
$filter = "fps=$GifFps,scale=${GifWidth}:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle"
& ffmpeg -hide_banner -loglevel error -y -framerate 30 -i $frames -frames:v ($GifSeconds * 30) -vf $filter -loop 0 $gif
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed making the GIF" }

foreach ($file in @($mp4, $gif)) {
    Write-Host ("{0}  {1:N1} MB" -f $file, ((Get-Item $file).Length / 1MB))
}
