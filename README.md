SalesLogix Project Upgrade Tool
====================================

## DESCRIPTION
This is a tool for analyzing and applying model changes.  This aids in gauging model upgrade effort as well as actually performing an upgrade with support for automatic merging of some types of files.
A powershell script is used as a front end to other utilities to reduce details the user has to manage and to make the process easier.

## Initial Setup
Sage.Platform.Upgrade.dll, UpgradeProject.exe, UpgradeProject.ps1, and ProjectReleaseInfo.db should be placed in a SalesLogix program install folder (This tooling depends on SLX version 7.5.4.).
Powershell script execution must be enabled on the machine by running "Set-ExecutionPolicy RemoteSigned" from a powershell command prompt.
A folder of all SalesLogix project backups and bundles must also be provided.  This folder must be referenced in the UpgradeProject.exe.config file in the ReleaseRepositoryPath appSettings value.
System.Data.SQLite.DLL must be copied to your install supportfiles folder (C:\Program Files (x86)\SalesLogix\SupportFiles).

## USE
 1. Open a powershell command prompt.
 1. Change to the SalesLogix install folder.
 1. Execute .\UpgradeProject [project path] [1|2|3]
[project path] represents the full path to your customized project.
A value of 1 builds a base project.  This create a starting point that represents your project without any customizations.
A value of 2 analyzes the differences between your customized project and the base project and reports on the differences, including items that can be automatically merged and those that will have to be manually merged.
A value of 3 does the same analysis as step 2, and also applies all file changes except for those that require manual merging.  This step requires a folder that already contains a project to be be merged to.  This project should be the version you are upgrading to.  This folder have the exact same path as your customized project path, but end in the name "Upgraded".
