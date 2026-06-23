$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$CsprojPath = Join-Path $ProjectRoot "src\YangTools.Revit\YangTools.Revit.csproj"
$ReleaseDir = Join-Path $ProjectRoot "YangTools_Release_v2.0"

Write-Host "1. Cleaning old release folder..."
if (Test-Path $ReleaseDir) { Remove-Item $ReleaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $ReleaseDir | Out-Null
New-Item -ItemType Directory -Path "$ReleaseDir\bin" | Out-Null
New-Item -ItemType Directory -Path "$ReleaseDir\scripts" | Out-Null

Write-Host "2. Building all versions..."
$Versions = @("2021", "2022", "2023", "2024", "2025", "2026", "2027")
foreach ($v in $Versions) {
    Write-Host "   Building Revit $v..."
    dotnet build $CsprojPath -c $v -p:Platform=x64 | Out-Null
    
    $BinSource = Join-Path $ProjectRoot "src\YangTools.Revit\bin\x64\$v"
    $BinDest = Join-Path $ReleaseDir "bin\$v"
    if (Test-Path $BinSource) {
        Copy-Item -Path $BinSource -Destination $BinDest -Recurse
    }
}

Write-Host "3. Copying addin manifest..."
Copy-Item (Join-Path $ProjectRoot "src\YangTools.Revit\YangTools.Revit.addin") "$ReleaseDir\"

Write-Host "4. Generating portable Installer script..."
$GuiScript = @"
Add-Type -AssemblyName PresentationFramework

[xml]`$XAML = @`"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Yang's Revit Installer (Portable)" Height="450" Width="400" WindowStartupLocation="CenterScreen" ResizeMode="NoResize"
        Background="#F8F9FA" FontFamily="Segoe UI">
    <Window.Resources>
        <Style TargetType="Button">
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}" CornerRadius="6" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Opacity" Value="0.85"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Window.Resources>
    <Grid Margin="25">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <TextBlock Text="Select Revit Versions to Install" FontWeight="SemiBold" FontSize="18" Foreground="#333333" Margin="0,0,0,20"/>
        
        <StackPanel Grid.Row="1" Name="CheckboxContainer" Margin="10,0,0,0">
            <CheckBox Content="Revit 2021" Tag="2021" IsChecked="True" Margin="0,6" FontSize="15" Foreground="#555555"/>
            <CheckBox Content="Revit 2022" Tag="2022" IsChecked="True" Margin="0,6" FontSize="15" Foreground="#555555"/>
            <CheckBox Content="Revit 2023" Tag="2023" IsChecked="True" Margin="0,6" FontSize="15" Foreground="#555555"/>
            <CheckBox Content="Revit 2024" Tag="2024" IsChecked="True" Margin="0,6" FontSize="15" Foreground="#555555"/>
            <CheckBox Content="Revit 2025" Tag="2025" IsChecked="True" Margin="0,6" FontSize="15" Foreground="#555555"/>
            <CheckBox Content="Revit 2026" Tag="2026" IsChecked="True" Margin="0,6" FontSize="15" Foreground="#555555"/>
            <CheckBox Content="Revit 2027" Tag="2027" IsChecked="True" Margin="0,6" FontSize="15" Foreground="#555555"/>
        </StackPanel>
        
        <Grid Grid.Row="2" Margin="0,25,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="15"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Button Name="InstallBtn" Content="Install" Background="#28A745" Foreground="White" FontWeight="Bold" FontSize="14" Padding="0,12"/>
            <Button Grid.Column="2" Name="UninstallBtn" Content="Clean &amp; Uninstall" Background="#DC3545" Foreground="White" FontWeight="Bold" FontSize="14" Padding="0,12"/>
        </Grid>
    </Grid>
</Window>
`"@

`$Reader = (New-Object System.Xml.XmlNodeReader `$XAML)
`$Form = [System.Windows.Markup.XamlReader]::Load(`$Reader)

`$InstallBtn = `$Form.FindName("InstallBtn")
`$UninstallBtn = `$Form.FindName("UninstallBtn")
`$CheckboxContainer = `$Form.FindName("CheckboxContainer")

`$ScriptDir = Split-Path -Parent `$MyInvocation.MyCommand.Definition
`$ReleaseRoot = Resolve-Path (Join-Path `$ScriptDir "..")

function Get-SelectedVersions {
    `$selected = @()
    foreach (`$cb in `$CheckboxContainer.Children) {
        if (`$cb.IsChecked) {
            `$selected += `$cb.Tag
        }
    }
    return `$selected
}

`$InstallBtn.Add_Click({
    `$selected = Get-SelectedVersions
    if (`$selected.Count -eq 0) {
        [System.Windows.MessageBox]::Show("Please select at least one version!", "Warning", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Warning)
        return
    }

    `$AppDataPath = [System.Environment]::GetFolderPath("ApplicationData")
    `$success = `$True

    foreach (`$v in `$selected) {
        `$SourceBin = Join-Path `$ReleaseRoot "bin\`$v"
        `$SourceAddin = Join-Path `$ReleaseRoot "YangTools.Revit.addin"
        
        `$DestDir = Join-Path `$AppDataPath "Autodesk\Revit\Addins\`$v\YangTools"
        `$DestAddin = Join-Path `$AppDataPath "Autodesk\Revit\Addins\`$v\YangTools.Revit.addin"
        
        try {
            if (-not (Test-Path `$SourceBin)) { continue }
            if (Test-Path `$DestDir) { Remove-Item `$DestDir -Recurse -Force }
            New-Item -ItemType Directory -Path `$DestDir | Out-Null
            Copy-Item -Path "`$SourceBin\*" -Destination `$DestDir -Recurse -Force
            Copy-Item -Path `$SourceAddin -Destination `$DestAddin -Force
        } catch {
            `$success = `$False
        }
    }

    if (`$success) {
        [System.Windows.MessageBox]::Show("Installation successful! Please restart Revit.", "Success", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information)
    } else {
        [System.Windows.MessageBox]::Show("Error during installation. Make sure Revit is closed.", "Error", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error)
    }
})

`$UninstallBtn.Add_Click({
    `$selected = Get-SelectedVersions
    if (`$selected.Count -eq 0) {
        [System.Windows.MessageBox]::Show("Please select at least one version!", "Warning", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Warning)
        return
    }

    `$AppDataPath = [System.Environment]::GetFolderPath("ApplicationData")
    `$success = `$True

    foreach (`$v in `$selected) {
        `$AddinDir = Join-Path `$AppDataPath "Autodesk\Revit\Addins\`$v\YangTools"
        `$AddinFile = Join-Path `$AppDataPath "Autodesk\Revit\Addins\`$v\YangTools.Revit.addin"
        
        try {
            if (Test-Path `$AddinDir) { Remove-Item -Path `$AddinDir -Recurse -Force }
            if (Test-Path `$AddinFile) { Remove-Item -Path `$AddinFile -Force }
        } catch {
            `$success = `$False
        }
    }

    if (`$success) {
        [System.Windows.MessageBox]::Show("Uninstall complete!", "Success", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information)
    } else {
        [System.Windows.MessageBox]::Show("Error during uninstall. Make sure Revit is closed.", "Error", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error)
    }
})

`$Form.ShowDialog() | Out-Null
"@

[System.IO.File]::WriteAllText("$ReleaseDir\scripts\InstallGUI.ps1", $GuiScript, [System.Text.Encoding]::UTF8)

$CmdScript = "@echo off`r`ntitle YangTools Portable Installer`r`npowershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File `"%~dp0scripts\InstallGUI.ps1`""
[System.IO.File]::WriteAllText("$ReleaseDir\Yang_Installer.cmd", $CmdScript, [System.Text.Encoding]::UTF8)

Write-Host "5. Zipping the release package..."
$ZipPath = Join-Path $ProjectRoot "YangTools_Release_v2.0.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath }
Compress-Archive -Path "$ReleaseDir\*" -DestinationPath $ZipPath

Write-Host "Done! Release package created at: $ZipPath" -ForegroundColor Green
