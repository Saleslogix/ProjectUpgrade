SalesLogix Project Upgrade Tool
====================================

Requires 7.5.4 installation to run, but can be used with any 7.5.x project.  
All necessary binaries, project backups and bundles are available at the regular business partner ftp site under /Upgrade_Tools/ProjectUpgrade/.  

## DESCRIPTION
This is a tool for analyzing and applying model changes.  This aids in gauging model upgrade effort as well as actually performing an upgrade with support for automatic merging of some types of files.
A powershell script is used as a front end to other utilities to reduce details the user has to manage and to make the process easier.

## Initial Setup
Please note that C:\Program Files\SalesLogix should be used in the instructions below instead of C:\Program Files (x86)\SalesLogix if you are using a 32-bit version of Windows.  
1. Get and unzip ProjectUpgradeBinaries.zip and SlxOfficialReleases.zip from the regular business partner ftp site under /Upgrade_Tools/ProjectUpgrade/.  
2. Unzip SlxOfficialReleases.zip to a folder of your choosing.  This zip contains all SalesLogix project backups and bundles going back to 7.5.0.  
3. Place UpgradeProject.exe, UpgradeProject.exe.config, UpgradeProject.ps1, and ProjectReleaseInfo.db in the folder C:\Program Files (x86)\SalesLogix\SupportFiles.  
4. Place Sage.Platform.Upgrade.dll in the folder C:\Program Files (x86)\SalesLogix\Platform.  
5. Place System.Data.SQLite.DLL in the folder C:\Program Files (x86)\SalesLogix\SupportFiles.  
6. Enable powershell script execution by opening a powershell command prompt (powershell.exe) and typing the following: Set-ExecutionPolicy RemoteSigned.  
7. Update UpgradeProject.exe.config to reference the folder you unzipped.  The full folder path should be placed in the "ReleaseRepositoryPath" appsetting.

## USE
 1. Open a powershell command prompt (powershell.exe).  
 2. Change to the SalesLogix install folder (cd "C:\Program Files (x86)\SalesLogix").  
 3. Execute .\UpgradeProject [project path] [0|1|2|3]  
[project path] represents the full path to your customized project.  
A value of 0 identifies the project version and the SalesLogix bundles that have been applied.  
A value of 1 builds a base project.  This creates a starting point that represents your project without any customizations.  
A value of 2 analyzes the differences between your customized project and the base project and reports on the differences, including items that can be automatically merged and those that will have to be manually merged.  
A value of 3 does the same analysis as step 2, and also applies all file changes except for those that require manual merging.  This step requires a folder that already contains a project to be be merged to.  This project should be the version you are upgrading to.  This folder have the exact same path as your customized project path, but end in the name "Upgraded".  
