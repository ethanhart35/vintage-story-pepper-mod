param(
    [string]$Type = "",
    [string]$Stages = "",
    [double]$Scale = 1.25,
    [switch]$NoBackup,
    [switch]$DryRun,
    [switch]$Cli
)

$ErrorActionPreference = "Stop"

$PepperTypes = @(
    "jalapeno",
    "habanero",
    "serrano",
    "cayenne",
    "poblano",
    "bell-pepper",
    "banana-pepper",
    "ghost-pepper"
)

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-StageNumbers {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text) -or $Text.Trim().ToLowerInvariant() -eq "all") {
        return 1..11
    }

    $numbers = New-Object System.Collections.Generic.List[int]

    foreach ($part in $Text.Split(",")) {
        $trimmed = $part.Trim()
        if ($trimmed -match "^(\d+)\s*-\s*(\d+)$") {
            $start = [int]$Matches[1]
            $end = [int]$Matches[2]
            if ($end -lt $start) {
                throw "Invalid stage range '$trimmed'."
            }
            for ($i = $start; $i -le $end; $i++) {
                $numbers.Add($i)
            }
        } elseif ($trimmed -match "^\d+$") {
            $numbers.Add([int]$trimmed)
        } else {
            throw "Invalid stage entry '$trimmed'. Use examples like 1-8 or 8,10,11."
        }
    }

    return $numbers | Sort-Object -Unique
}

function Test-HiddenRootElement {
    param($Element)

    if ($Element.PSObject.Properties.Name -notcontains "name") {
        return $false
    }

    if ($Element.name -notmatch "Root$") {
        return $false
    }

    foreach ($field in @("from", "to")) {
        if ($Element.PSObject.Properties.Name -notcontains $field) {
            return $false
        }

        $values = @($Element.$field)
        if ($values.Count -lt 3) {
            return $false
        }

        if ([double]$values[0] -ne 0 -or [double]$values[1] -ne 0 -or [double]$values[2] -ne 0) {
            return $false
        }
    }

    if ($Element.PSObject.Properties.Name -contains "faces") {
        return ($Element.faces.PSObject.Properties.Count -eq 0)
    }

    return $true
}

function Set-ScaledVector {
    param(
        $Object,
        [string]$FieldName,
        [double[]]$Origin,
        [double]$ScaleFactor
    )

    if ($Object.PSObject.Properties.Name -notcontains $FieldName) {
        return
    }

    $values = @($Object.$FieldName)
    if ($values.Count -lt 3) {
        return
    }

    $Object.$FieldName = @(
        [Math]::Round($Origin[0] + (([double]$values[0] - $Origin[0]) * $ScaleFactor), 6),
        [Math]::Round($Origin[1] + (([double]$values[1] - $Origin[1]) * $ScaleFactor), 6),
        [Math]::Round($Origin[2] + (([double]$values[2] - $Origin[2]) * $ScaleFactor), 6)
    )
}

function Scale-ShapeElement {
    param(
        $Element,
        [double[]]$Origin,
        [double]$ScaleFactor
    )

    $skipThisElement = Test-HiddenRootElement -Element $Element

    if (-not $skipThisElement) {
        Set-ScaledVector -Object $Element -FieldName "from" -Origin $Origin -ScaleFactor $ScaleFactor
        Set-ScaledVector -Object $Element -FieldName "to" -Origin $Origin -ScaleFactor $ScaleFactor
        Set-ScaledVector -Object $Element -FieldName "rotationOrigin" -Origin $Origin -ScaleFactor $ScaleFactor
    }

    if ($Element.PSObject.Properties.Name -contains "children" -and $Element.children) {
        foreach ($child in $Element.children) {
            Scale-ShapeElement -Element $child -Origin $Origin -ScaleFactor $ScaleFactor
        }
    }
}

function Copy-Backup {
    param(
        [string]$RepoRoot,
        [string]$FilePath,
        [string]$BackupRoot
    )

    $relative = $FilePath.Substring($RepoRoot.Length + 1)
    $backupPath = Join-Path $BackupRoot $relative
    $backupDir = Split-Path -Parent $backupPath

    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    Copy-Item -LiteralPath $FilePath -Destination $backupPath -Force

    return $backupPath
}

function Invoke-PepperModelScale {
    param(
        [string]$TypeText,
        [string]$StageText,
        [double]$ScaleFactor,
        [bool]$CreateBackup,
        [bool]$PreviewOnly
    )

    if ($ScaleFactor -le 0) {
        throw "Scale must be greater than 0."
    }

    $repoRoot = Get-RepoRoot
    $shapeRoot = Join-Path $repoRoot "assets\peppermod\shapes\block\plant\crop"
    $origin = @(8.0, 0.0, 8.0)
    $stageNumbers = @(Get-StageNumbers -Text $StageText)

    if ([string]::IsNullOrWhiteSpace($TypeText) -or $TypeText -eq "(all types)") {
        $types = $PepperTypes
    } else {
        $types = @($TypeText)
    }

    foreach ($pepperType in $types) {
        if ($PepperTypes -notcontains $pepperType) {
            throw "Unknown pepper type '$pepperType'."
        }
    }

    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupRoot = Join-Path $repoRoot "backups\model-scaler\$stamp"
    $changed = New-Object System.Collections.Generic.List[string]
    $missing = New-Object System.Collections.Generic.List[string]

    foreach ($pepperType in $types) {
        foreach ($stage in $stageNumbers) {
            $path = Join-Path $shapeRoot "$pepperType\stage$stage.json"

            if (-not (Test-Path $path)) {
                $missing.Add($path)
                continue
            }

            if ($PreviewOnly) {
                $changed.Add("[dry run] $path")
                continue
            }

            if ($CreateBackup) {
                Copy-Backup -RepoRoot $repoRoot -FilePath (Resolve-Path $path).Path -BackupRoot $backupRoot | Out-Null
            }

            $shape = Get-Content -Raw $path | ConvertFrom-Json
            foreach ($element in $shape.elements) {
                Scale-ShapeElement -Element $element -Origin $origin -ScaleFactor $ScaleFactor
            }

            $shape | ConvertTo-Json -Depth 100 | Set-Content -Encoding UTF8 $path
            $changed.Add($path)
        }
    }

    return [pscustomobject]@{
        Changed = @($changed)
        Missing = @($missing)
        BackupRoot = if ($CreateBackup -and -not $PreviewOnly) { $backupRoot } else { "" }
    }
}

function Start-ScalerGui {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    [System.Windows.Forms.Application]::EnableVisualStyles()

    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Pepper Model Scaler"
    $form.StartPosition = "CenterScreen"
    $form.Size = New-Object System.Drawing.Size(560, 420)
    $form.FormBorderStyle = "FixedDialog"
    $form.MaximizeBox = $false

    $typeLabel = New-Object System.Windows.Forms.Label
    $typeLabel.Text = "Pepper"
    $typeLabel.Location = New-Object System.Drawing.Point(18, 22)
    $typeLabel.Size = New-Object System.Drawing.Size(110, 22)
    $form.Controls.Add($typeLabel)

    $typeCombo = New-Object System.Windows.Forms.ComboBox
    $typeCombo.DropDownStyle = "DropDownList"
    [void]$typeCombo.Items.Add("(all types)")
    foreach ($pepperType in $PepperTypes) {
        [void]$typeCombo.Items.Add($pepperType)
    }
    $typeCombo.SelectedItem = "jalapeno"
    $typeCombo.Location = New-Object System.Drawing.Point(140, 20)
    $typeCombo.Size = New-Object System.Drawing.Size(180, 24)
    $form.Controls.Add($typeCombo)

    $stageLabel = New-Object System.Windows.Forms.Label
    $stageLabel.Text = "Stages"
    $stageLabel.Location = New-Object System.Drawing.Point(18, 58)
    $stageLabel.Size = New-Object System.Drawing.Size(110, 22)
    $form.Controls.Add($stageLabel)

    $stageBox = New-Object System.Windows.Forms.TextBox
    $stageBox.Text = "1-11"
    $stageBox.Location = New-Object System.Drawing.Point(140, 56)
    $stageBox.Size = New-Object System.Drawing.Size(180, 24)
    $form.Controls.Add($stageBox)

    $scaleLabel = New-Object System.Windows.Forms.Label
    $scaleLabel.Text = "Scale"
    $scaleLabel.Location = New-Object System.Drawing.Point(18, 94)
    $scaleLabel.Size = New-Object System.Drawing.Size(110, 22)
    $form.Controls.Add($scaleLabel)

    $scaleBox = New-Object System.Windows.Forms.TextBox
    $scaleBox.Text = "1.25"
    $scaleBox.Location = New-Object System.Drawing.Point(140, 92)
    $scaleBox.Size = New-Object System.Drawing.Size(180, 24)
    $form.Controls.Add($scaleBox)

    $backupCheck = New-Object System.Windows.Forms.CheckBox
    $backupCheck.Text = "Create backup"
    $backupCheck.Checked = $true
    $backupCheck.Location = New-Object System.Drawing.Point(140, 126)
    $backupCheck.Size = New-Object System.Drawing.Size(180, 24)
    $form.Controls.Add($backupCheck)

    $applyButton = New-Object System.Windows.Forms.Button
    $applyButton.Text = "Scale Models"
    $applyButton.Location = New-Object System.Drawing.Point(340, 20)
    $applyButton.Size = New-Object System.Drawing.Size(170, 34)
    $form.Controls.Add($applyButton)

    $dryRunButton = New-Object System.Windows.Forms.Button
    $dryRunButton.Text = "Preview File List"
    $dryRunButton.Location = New-Object System.Drawing.Point(340, 62)
    $dryRunButton.Size = New-Object System.Drawing.Size(170, 34)
    $form.Controls.Add($dryRunButton)

    $closeButton = New-Object System.Windows.Forms.Button
    $closeButton.Text = "Close"
    $closeButton.Location = New-Object System.Drawing.Point(340, 104)
    $closeButton.Size = New-Object System.Drawing.Size(170, 34)
    $closeButton.Add_Click({ $form.Close() })
    $form.Controls.Add($closeButton)

    $logBox = New-Object System.Windows.Forms.TextBox
    $logBox.Multiline = $true
    $logBox.ScrollBars = "Vertical"
    $logBox.ReadOnly = $true
    $logBox.Location = New-Object System.Drawing.Point(18, 166)
    $logBox.Size = New-Object System.Drawing.Size(500, 190)
    $form.Controls.Add($logBox)

    function Run-GuiScale {
        param([bool]$PreviewOnly)

        try {
            $scaleFactor = [double]$scaleBox.Text
            $result = Invoke-PepperModelScale `
                -TypeText $typeCombo.SelectedItem `
                -StageText $stageBox.Text `
                -ScaleFactor $scaleFactor `
                -CreateBackup $backupCheck.Checked `
                -PreviewOnly $PreviewOnly

            $lines = New-Object System.Collections.Generic.List[string]
            if ($PreviewOnly) {
                $lines.Add("Preview only. No files changed.")
            } else {
                $lines.Add("Scaled files:")
            }

            foreach ($path in $result.Changed) {
                $lines.Add($path)
            }

            if ($result.Missing.Count -gt 0) {
                $lines.Add("")
                $lines.Add("Missing files:")
                foreach ($path in $result.Missing) {
                    $lines.Add($path)
                }
            }

            if ($result.BackupRoot) {
                $lines.Add("")
                $lines.Add("Backup:")
                $lines.Add($result.BackupRoot)
            }

            $logBox.Text = ($lines -join [Environment]::NewLine)
        } catch {
            $logBox.Text = $_.Exception.Message
        }
    }

    $applyButton.Add_Click({ Run-GuiScale -PreviewOnly $false })
    $dryRunButton.Add_Click({ Run-GuiScale -PreviewOnly $true })

    [void]$form.ShowDialog()
}

if ($Cli -or $Type -or $Stages) {
    if ([string]::IsNullOrWhiteSpace($Type)) {
        $Type = "jalapeno"
    }
    if ([string]::IsNullOrWhiteSpace($Stages)) {
        $Stages = "1-11"
    }

    $result = Invoke-PepperModelScale `
        -TypeText $Type `
        -StageText $Stages `
        -ScaleFactor $Scale `
        -CreateBackup (-not $NoBackup) `
        -PreviewOnly $DryRun

    if ($DryRun) {
        "Preview only. No files changed."
    } else {
        "Scaled files:"
    }

    $result.Changed

    if ($result.Missing.Count -gt 0) {
        ""
        "Missing files:"
        $result.Missing
    }

    if ($result.BackupRoot) {
        ""
        "Backup:"
        $result.BackupRoot
    }
} else {
    Start-ScalerGui
}
