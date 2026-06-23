Add-Type -AssemblyName PresentationFramework

[xml]$XAML = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Yang's Revit Installer" Height="450" Width="400" WindowStartupLocation="CenterScreen" ResizeMode="NoResize"
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
        
        <TextBlock Text="Select Revit Versions to Deploy" FontWeight="SemiBold" FontSize="18" Foreground="#333333" Margin="0,0,0,20"/>
        
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
            <Button Name="InstallBtn" Content="Install &amp; Deploy" Background="#28A745" Foreground="White" FontWeight="Bold" FontSize="14" Padding="0,12"/>
            <Button Grid.Column="2" Name="UninstallBtn" Content="Clean &amp; Uninstall" Background="#DC3545" Foreground="White" FontWeight="Bold" FontSize="14" Padding="0,12"/>
        </Grid>
    </Grid>
</Window>
"@

$Reader = (New-Object System.Xml.XmlNodeReader $XAML)
$Form = [System.Windows.Markup.XamlReader]::Load($Reader)

$InstallBtn = $Form.FindName("InstallBtn")
$UninstallBtn = $Form.FindName("UninstallBtn")
$CheckboxContainer = $Form.FindName("CheckboxContainer")

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectDir = Resolve-Path (Join-Path $ScriptDir "..")
$CsprojPath = Join-Path $ProjectDir "src\YangTools.Revit\YangTools.Revit.csproj"

function Get-SelectedVersions {
    $selected = @()
    foreach ($cb in $CheckboxContainer.Children) {
        if ($cb.IsChecked) {
            $selected += $cb.Tag
        }
    }
    return $selected
}

$InstallBtn.Add_Click({
    $selected = Get-SelectedVersions
    if ($selected.Count -eq 0) {
        [System.Windows.MessageBox]::Show("Please select at least one version!", "Warning", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Warning)
        return
    }

    $InstallBtn.IsEnabled = $False
    $UninstallBtn.IsEnabled = $False

    $cmdPath = Join-Path $env:TEMP "YangTools_Deploy.cmd"
    $cmdContent = "@echo off`r`ntitle YangTools Deployment`r`n"
    foreach ($v in $selected) {
        $cmdContent += "echo ---------------------------------------------------`r`n"
        $cmdContent += "echo Deploying YangTools for Revit $v ...`r`n"
        $cmdContent += "dotnet build `"$CsprojPath`" -c $v -p:Platform=x64`r`n"
        $cmdContent += "if %ERRORLEVEL% NEQ 0 echo Error deploying Revit $v!`r`n"
    }
    $cmdContent += "echo ---------------------------------------------------`r`n"
    $cmdContent += "echo.`r`necho All selected versions have been processed.`r`n"
    $cmdContent += "echo You can now close this window.`r`npause`r`n"
    
    [System.IO.File]::WriteAllText($cmdPath, $cmdContent, [System.Text.Encoding]::Default)
    
    Start-Process -FilePath $cmdPath

    [System.Windows.MessageBox]::Show("Deployment task launched! Please check the newly opened console window for progress.", "Success", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information)

    $InstallBtn.IsEnabled = $True
    $UninstallBtn.IsEnabled = $True
})

$UninstallBtn.Add_Click({
    $selected = Get-SelectedVersions
    if ($selected.Count -eq 0) {
        [System.Windows.MessageBox]::Show("Please select at least one version!", "Warning", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Warning)
        return
    }

    $AppDataPath = [System.Environment]::GetFolderPath("ApplicationData")
    $success = $True

    foreach ($v in $selected) {
        $AddinDir = Join-Path $AppDataPath "Autodesk\Revit\Addins\$v\YangTools"
        $AddinFile = Join-Path $AppDataPath "Autodesk\Revit\Addins\$v\YangTools.Revit.addin"
        
        try {
            if (Test-Path $AddinDir) { Remove-Item -Path $AddinDir -Recurse -Force }
            if (Test-Path $AddinFile) { Remove-Item -Path $AddinFile -Force }
        } catch {
            $success = $False
        }
    }

    if ($success) {
        [System.Windows.MessageBox]::Show("Uninstall complete!", "Success", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information)
    } else {
        [System.Windows.MessageBox]::Show("Error during uninstall. Make sure Revit is fully closed and try again.", "Error", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error)
    }
})

$Form.ShowDialog() | Out-Null
