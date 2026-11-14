<#
.SYNOPSIS
    从角色原画重切部位图，为关节补出血（重叠），并生成关节补片与 attachments 配置草案。

.DESCRIPTION
    只读取角色原画（baiyy.png），不修改现有 images/ 与 attachments.json，
    产物全部写入 <角色包>/images_bleed/，供人工目检后再替换。

    几何依据：docs/设计/无缝变形与自主控制方案.md
      - 4.2.1  各切片的裁切矩形、哪些关节可以重切
      - 附录 B  外扩后 pivot 的重算公式（offset 保持不变）

    肩、髋在原画里被上层遮住，没有内容可重切，本脚本只生成补片占位图。

.PARAMETER PackageDir
    角色包目录，默认为仓库内的 白摇摇。

.PARAMETER NeckBleed
    颈部出血像素数（body 顶边上扩）。

.PARAMETER ElbowBleed
    肘部出血像素数（小臂内侧端外扩）。

.PARAMETER KneeBleed
    膝部出血像素数（小腿上端上扩）。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File "H:\Visual Studio xiangm\KfuPet-Tool\docs\rebleed.ps1"

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File rebleed.ps1 -PackageDir "H:\Visual Studio xiangm\KfuPet\Characters\白摇摇-Beta" -NeckBleed 20
#>
[CmdletBinding()]
param(
    [string]$PackageDir = '',
    [string]$MasterFile = 'baiyy.png',
    [string]$OutSubDir  = 'images_bleed',
    [int]$NeckBleed  = 16,
    [int]$ElbowBleed = 41,
    [int]$KneeBleed  = 44,
    [switch]$SkipPatches
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$toolsDir = Split-Path -Parent $PSCommandPath
if ([string]::IsNullOrWhiteSpace($PackageDir)) {
    # 脚本已移出主仓库，默认按两种布局找角色包：
    #   1) 仍放在 <仓库>\tools 下
    #   2) 放在 KfuPet-Tool\docs 下，主仓库与 KfuPet-Tool 同级
    $candidates = @(
        (Join-Path $toolsDir '..\Characters\白摇摇'),
        (Join-Path $toolsDir '..\..\KfuPet\Characters\白摇摇')
    )
    $PackageDir = $candidates | Where-Object { Test-Path (Join-Path $_ 'character.json') } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($PackageDir)) {
        throw '未找到角色包，请用 -PackageDir 指定，例如 -PackageDir "H:\Visual Studio xiangm\KfuPet\Characters\白摇摇"'
    }
}
$PackageDir = (Resolve-Path $PackageDir).Path

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition (Get-Content (Join-Path $toolsDir 'Rebleed.cs.txt') -Raw -Encoding UTF8) -ReferencedAssemblies System.Drawing

# ---------------------------------------------------------------------------
# 1. 事实表：各切片在原画中的裁切矩形，以及需要外扩的边
#    矩形与方向来自文档 4.2.1 的实测结果
# ---------------------------------------------------------------------------
$parts = @(
    @{ File = 'head.png';            X = 569;  Y = 55;   W = 419; H = 399; PivotX = 0.5; PivotY = 1.0; Edge = $null;   Bleed = 0 },
    @{ File = 'body.png';            X = 570;  Y = 450;  W = 395; H = 785; PivotX = 0.5; PivotY = 0.904459; Edge = 'top';   Bleed = $NeckBleed },
    @{ File = 'arm_left_upper.png';  X = 903;  Y = 497;  W = 267; H = 142; PivotX = 0.0; PivotY = 0.5; Edge = $null;   Bleed = 0 },
    @{ File = 'arm_left_lower.png';  X = 1164; Y = 535;  W = 336; H = 78;  PivotX = 0.0; PivotY = 0.5; Edge = 'left';  Bleed = $ElbowBleed },
    @{ File = 'arm_right_upper.png'; X = 311;  Y = 491;  W = 319; H = 147; PivotX = 1.0; PivotY = 0.5; Edge = $null;   Bleed = 0 },
    @{ File = 'arm_right_lower.png'; X = 36;   Y = 535;  W = 277; H = 78;  PivotX = 1.0; PivotY = 0.5; Edge = 'right'; Bleed = $ElbowBleed },
    @{ File = 'leg_left_upper.png';  X = 794;  Y = 1178; W = 128; H = 286; PivotX = 0.5; PivotY = 0.0; Edge = $null;   Bleed = 0 },
    @{ File = 'leg_left_lower.png';  X = 781;  Y = 1460; W = 159; H = 508; PivotX = 0.5; PivotY = 0.0;      Edge = 'top';   Bleed = $KneeBleed },
    @{ File = 'leg_right_upper.png'; X = 613;  Y = 1176; W = 128; H = 283; PivotX = 0.5; PivotY = 0.0;      Edge = $null;   Bleed = 0 },
    @{ File = 'leg_right_lower.png'; X = 594;  Y = 1456; W = 160; H = 512; PivotX = 0.5; PivotY = 0.0;      Edge = 'top';   Bleed = $KneeBleed }
)

# 2. 补片：原画里被遮挡、无法重切的两个关节。
#    直接从「手臂/大腿自己的切片」里切一角（不是从原画切），保证补片的 alpha
#    是原切片 alpha 的子集 —— 静止姿态下被原图完全盖住，不会露出轮廓；
#    骨骼转开后才露出来补住空洞。Offset 已按「挂在 body 上、静止时与原图重合」换算。
$patches = @(
    @{ File = 'patch_shoulder_l.png'; Name = '肩部补片（画面左侧）'; SrcFile = 'arm_right_upper.png'; SrcX = 169; SrcY = 0; W = 150; H = 147; PivotX = 1.0; PivotY = 0.5; OffsetX = -29.47; OffsetY = -127.6;  Z = 21 },
    @{ File = 'patch_shoulder_r.png'; Name = '肩部补片（画面右侧）'; SrcFile = 'arm_left_upper.png';  SrcX = 0;   SrcY = 0; W = 150; H = 142; PivotX = 0.0; PivotY = 0.5; OffsetX = 29.04;  OffsetY = -126.85; Z = 22 },
    @{ File = 'patch_hip_l.png';      Name = '髋部补片（画面左侧）'; SrcFile = 'leg_right_upper.png'; SrcX = 0;   SrcY = 0; W = 128; H = 70;  PivotX = 0.5; PivotY = 0.0; OffsetX = -19.4;  OffsetY = 3.46;    Z = 9 },
    @{ File = 'patch_hip_r.png';      Name = '髋部补片（画面右侧）'; SrcFile = 'leg_left_upper.png';  SrcX = 0;   SrcY = 0; W = 128; H = 70;  PivotX = 0.5; PivotY = 0.0; OffsetX = 19.4;   OffsetY = 3.89;    Z = 9 }
)

# ---------------------------------------------------------------------------
# 3. 准备
# ---------------------------------------------------------------------------
$PackageDir = (Resolve-Path $PackageDir).Path
$masterPath = Join-Path $PackageDir $MasterFile
$imagesDir  = Join-Path $PackageDir 'images'
$outDir     = Join-Path $PackageDir $OutSubDir
$manifestPath = Join-Path $PackageDir 'attachments.json'

if (-not (Test-Path $masterPath)) { throw "master art not found: $masterPath" }
if (-not (Test-Path $imagesDir))  { throw "images folder not found: $imagesDir" }
if (-not (Test-Path $outDir))     { New-Item -ItemType Directory -Path $outDir | Out-Null }

$master = [RebleedImage]::Load($masterPath)
Write-Host ("原画：{0}  {1}x{2}" -f $MasterFile, $master.W, $master.H) -ForegroundColor Cyan

$dpi = 350.0
$results = @{}
$report = New-Object System.Collections.Generic.List[string]

# ---------------------------------------------------------------------------
# 4. 重切
# ---------------------------------------------------------------------------
foreach ($p in $parts) {
    $x = $p.X; $y = $p.Y; $w = $p.W; $h = $p.H
    $pivotX = $p.PivotX; $pivotY = $p.PivotY

    if ($p.Edge -and $p.Bleed -gt 0) {
        $b = $p.Bleed
        switch ($p.Edge) {
            'top' {
                $y = $y - $b; $h = $h + $b
                $pivotY = ($p.PivotY * $p.H + $b) / $h
            }
            'bottom' {
                $h = $h + $b
                $pivotY = ($p.PivotY * $p.H) / $h
            }
            'left' {
                $x = $x - $b; $w = $w + $b
                $pivotX = ($p.PivotX * $p.W + $b) / $w
            }
            'right' {
                $w = $w + $b
                $pivotX = ($p.PivotX * $p.W) / $w
            }
        }
    }

    $img = [Rebleed]::Crop($master, $x, $y, $w, $h)
    $img.Save((Join-Path $outDir $p.File), $dpi)

    $results[$p.File] = @{ PivotX = $pivotX; PivotY = $pivotY; W = $w; H = $h }

    $tag = 'as-is'
    if ($p.Edge -and $p.Bleed -gt 0) { $tag = "bleed $($p.Edge) +$($p.Bleed)px" }
    $report.Add(("{0,-22} {1,4} x {2,-4}  rect ({3},{4})  pivot ({5:F6}, {6:F6})  [{7}]" -f
        $p.File, $w, $h, $x, $y, $pivotX, $pivotY, $tag))
}

Write-Host ""
Write-Host "部位图输出目录：$outDir" -ForegroundColor Green
foreach ($line in $report) { Write-Host "  $line" }

# ---------------------------------------------------------------------------
# 5. 出血校验：外扩出来的那一圈必须真的有像素
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "出血校验（外扩边的不透明像素数，0 表示没裁到内容）：" -ForegroundColor Cyan
foreach ($p in $parts) {
    if (-not $p.Edge -or $p.Bleed -le 0) { continue }
    $img = [RebleedImage]::Load((Join-Path $outDir $p.File))
    $half = [int]($p.Bleed / 2)
    switch ($p.Edge) {
        'top'    { $n = [Rebleed]::RowCount($img, $half) }
        'bottom' { $n = [Rebleed]::RowCount($img, $img.H - $half - 1) }
        'left'   { $n = [Rebleed]::ColCount($img, $half) }
        'right'  { $n = [Rebleed]::ColCount($img, $img.W - $half - 1) }
    }
    $color = 'Red'
    if ($n -gt 0) { $color = 'Green' }
    Write-Host ("  {0,-22} {1} +{2}px -> {3} px" -f $p.File, $p.Edge, $p.Bleed, $n) -ForegroundColor $color
}

# ---------------------------------------------------------------------------
# 6. 补片（占位色块，后续可换成手绘）
# ---------------------------------------------------------------------------
if (-not $SkipPatches) {
    Write-Host ""
    Write-Host "补片（从对应切片内部切角，alpha 不超出原图）：" -ForegroundColor Cyan
    foreach ($q in $patches) {
        $src = [RebleedImage]::Load((Join-Path $outDir $q.SrcFile))
        $img = [Rebleed]::Crop($src, $q.SrcX, $q.SrcY, $q.W, $q.H)
        $img.Save((Join-Path $outDir $q.File), $dpi)
        Write-Host ("  {0,-24} {1} x {2}  <= {3} ({4},{5})" -f $q.File, $q.W, $q.H, $q.SrcFile, $q.SrcX, $q.SrcY)
    }
}

# ---------------------------------------------------------------------------
# 7. 生成 attachments 草案（资源路径按最终的 images/ 写）
# ---------------------------------------------------------------------------
$inv = [System.Globalization.CultureInfo]::InvariantCulture
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('{')
[void]$sb.AppendLine(('  "version": "{0}",' -f $manifest.version))
[void]$sb.AppendLine(('  "scale": {0},' -f ([double]$manifest.scale).ToString('0.#####', $inv)))
[void]$sb.AppendLine('  "attachments": [')

$entries = New-Object System.Collections.Generic.List[string]

foreach ($a in $manifest.attachments) {
    $res  = [string]$a.resource
    $file = Split-Path $res -Leaf
    $r    = $results[$file]

    $px = [double]$a.pivot[0]
    $py = [double]$a.pivot[1]
    if ($r) { $px = $r.PivotX; $py = $r.PivotY }

    $ox = ([double]$a.offset[0]).ToString('0.##', $inv)
    $oy = ([double]$a.offset[1]).ToString('0.##', $inv)
    $pvs = $px.ToString('0.######', $inv)
    $pvy = $py.ToString('0.######', $inv)
    $z   = [int]$a.zOrder
    $vis = 'true'
    if (-not $a.visible) { $vis = 'false' }

    $entries.Add((@(
        '    {'
        ('      "id": "{0}",' -f $a.id)
        ('      "boneId": "{0}",' -f $a.boneId)
        ('      "name": "{0}",' -f $a.name)
        ('      "resource": "images/{0}",' -f $file)
        ('      "pivot": [{0}, {1}],' -f $pvs, $pvy)
        ('      "offset": [{0}, {1}],' -f $ox, $oy)
        ('      "zOrder": {0},' -f $z)
        ('      "visible": {0}' -f $vis)
        '    }'
    )) -join "`r`n")
}

if (-not $SkipPatches) {
    foreach ($q in $patches) {
        $id = [System.IO.Path]::GetFileNameWithoutExtension($q.File)
        $entries.Add((@(
            '    {'
            ('      "id": "{0}",' -f $id)
            '      "boneId": "body",'
            ('      "name": "{0}",' -f $q.Name)
            ('      "resource": "images/{0}",' -f $q.File)
            ('      "pivot": [{0}, {1}],' -f $q.PivotX, $q.PivotY)
            ('      "offset": [{0}, {1}],' -f $q.OffsetX, $q.OffsetY)
            ('      "zOrder": {0},' -f $q.Z)
            '      "visible": true'
            '    }'
        )) -join "`r`n")
    }
}

[void]$sb.AppendLine(($entries -join (",`r`n")))
[void]$sb.AppendLine('  ]')
[void]$sb.AppendLine('}')

$draftPath = Join-Path $outDir 'attachments_bleed.json'
[System.IO.File]::WriteAllText($draftPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host "配置草案：$draftPath" -ForegroundColor Green

Write-Host ""
Write-Host "下一步（确认无误后再做）：" -ForegroundColor Yellow
Write-Host "  1. 目检 $OutSubDir 里的图，重点看外扩那一圈是否自然" -ForegroundColor Yellow
Write-Host "  2. 把 $OutSubDir 里的 PNG 覆盖到 images\" -ForegroundColor Yellow
Write-Host "  3. 把 attachments_bleed.json 改名覆盖 attachments.json" -ForegroundColor Yellow
Write-Host "  4. 补片 offset 在开发者工具里配合调试线框微调" -ForegroundColor Yellow
