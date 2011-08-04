if ($args.count -ne 2)
{
	Write-Host "Usage:"
	Write-Host "$($MyInvocation.MyCommand) [SourceProjectPath] [0|1|2|3]"
	Write-Host
	Write-Host "0 = Identify project version and bundles applied"
	Write-Host "1 = Build Base Project"
	Write-Host "2 = Analyze Upgrade Effort"
	Write-Host "3 = Execute Upgrade"
	Write-Host
	Write-Host "Notes:"
	Write-Host "Build Base Project will build to the folder [SourceProjectPath] + 'Base'"
	Write-Host "Execute Upgrade expects the target project to exist at [SourceProjectPath] + 'Upgraded'" 
	exit
}

$sourcePath = $args[0]
$operation = $args[1]

if (!(Test-Path $sourcePath))
{
	Write-Host $args[0] " is not a valid project backup or folder"
	exit
}

$validOptions = ("0", "1", "2", "3")
if ($validOptions -notcontains $operation)
{
	Write-Host "invalid second paramter: " $operation
	exit
}

#normalize the sourcePath so it never includes the model name
$sourceDir = gci $sourcePath
if ("Model" -match $sourceDir.Name)
{
	$sourcePath = Split-Path $sourcePath -Parent
}

$scriptPath = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Definition)
cd $scriptPath

if ($sourcePath -like "*backup.zip")
{
	$backupName = [System.IO.Path]::GetFileName($sourcePath)
	$backupName = $backupName.SubString(0, $backupName.Length - 11)
	$newSourcePath = Split-Path $sourcePath -Parent
	$newSourcePath = $newSourcePath + "\" + $backupName
	
	#If $newSourcePath does not exist then restore it from backup
	if (!(Test-Path $newSourcePath\Model -PathType Container))
	{
		.\ProjectBackupRestore.exe /P:"$newSourcePath\Model" /B:"$sourcePath" /C:.\SampleBackupRestoreConfig.xml /O:Restore
	}
	
	$sourcePath = $newSourcePath
}

if ($operation -eq "0")
{
	.\UpgradeProject.exe /O:IdentifyProjectVersion /SP:"$($sourcePath)\Model"
}
elseif ($operation -eq "1")
{
	.\UpgradeProject.exe /O:BuildBaseProject /SP:"$($sourcePath)\Model" /BP:"$($sourcePath)Base\Model"
}
elseif ($operation -eq "2")
{
	$reportFilePath = "$sourcePath Upgrade Report.txt"
	.\UpgradeProject.exe /O:UpgradeReport /SP:"$($sourcePath)\Model" /BP:"$($sourcePath)Base\Model" > $reportFilePath
	Write-Host "Upgrade report output was written to $reportFilePath"
}
elseif ($operation -eq "3")
{
	.\UpgradeProject.exe /O:Upgrade /SP:"$($sourcePath)\Model" /BP:"$($sourcePath)Base\Model" /TP:"$($sourcePath)Upgraded\Model"
}
