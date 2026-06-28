# Independent oracle for the wallpaper mosaic geometry golden master.
#
# This script re-implements the documented layout rule (AlbumCoverFinder boundary 4:
# tile / column / row math + dest-rectangle grid) FROM THE SPEC, separately from the
# C# WallpaperRenderer.DescribeLayout it pins. The renderer test asserts that the C#
# output equals these files, so a match is a differential agreement between two
# independent implementations of the same rule, not a self-snapshot.
#
# Documented rule (see WallpaperRenderer.ComputeLayout / the design's boundary 4):
#   coversWide > 0 : cols = coversWide ; tile = max(32, floor(width / cols))
#   coversWide = 0 : tile = max(32, targetTilePx) ; cols = ceil(width / tile)
#   rows = ceil(height / tile)            (ceil so the bottom clips a partial tile)
#   dest[x,y] = (x*tile, y*tile, tile, tile), emitted row-major (draw order)
#
# To regenerate, dot-source this file in a PowerShell session and call the writer
# (keeps you inside your normal execution policy; review the diff before committing):
#   . .\generate-golden.ps1
# This is a human-approval checkpoint: eyeball the headline numbers (tile/cols/rows)
# against the rule before trusting a regenerated file.

function New-GoldenLayout {
    param([int]$Width, [int]$Height, [int]$TilePx, [int]$CoversWide)

    if ($CoversWide -gt 0) {
        $cols = $CoversWide
        $tile = [Math]::Max(32, [Math]::Floor($Width / $cols))
    } else {
        $tile = [Math]::Max(32, $TilePx)
        $cols = [Math]::Floor(($Width + $tile - 1) / $tile)   # ceil(width / tile)
    }
    $rows = [Math]::Floor(($Height + $tile - 1) / $tile)      # ceil(height / tile)
    $tile = [int]$tile; $cols = [int]$cols; $rows = [int]$rows
    $count = $cols * $rows

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.Append("canvas=${Width}x${Height} targetTilePx=$TilePx coversWide=$CoversWide`n")
    [void]$sb.Append("tile=$tile cols=$cols rows=$rows count=$count`n")
    $idx = 0
    for ($y = 0; $y -lt $rows; $y++) {
        for ($x = 0; $x -lt $cols; $x++) {
            $r = '{0:D3}' -f $idx
            $dx = $x * $tile; $dy = $y * $tile
            [void]$sb.Append("r$r col=$x row=$y dest=$dx,$dy,$tile,$tile`n")
            $idx++
        }
    }
    $s = $sb.ToString()
    if ($s.EndsWith("`n")) { $s = $s.Substring(0, $s.Length - 1) }
    return $s
}

$here = Split-Path -Parent $MyInvocation.MyCommand.Path

# Case A: 4K canvas, CoversWide = 12 (targetTilePx ignored when CoversWide > 0).
$a = New-GoldenLayout -Width 3840 -Height 2160 -TilePx 256 -CoversWide 12
[IO.File]::WriteAllText((Join-Path $here 'wallpaper_layout_3840x2160_w12.txt'), $a)

# Case B: 1080p canvas, CoversWide = 0 -> native tilePx = 256 drives the grid.
$b = New-GoldenLayout -Width 1920 -Height 1080 -TilePx 256 -CoversWide 0
[IO.File]::WriteAllText((Join-Path $here 'wallpaper_layout_1920x1080_w0_t256.txt'), $b)

Write-Output "Wrote golden masters:"
Write-Output ("  A 3840x2160 w12  : " + $a.Split("`n")[1])
Write-Output ("  B 1920x1080 w0   : " + $b.Split("`n")[1])
