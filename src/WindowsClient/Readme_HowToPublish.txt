Steps to get the NSIS install working
	1) Install the nuget package for NSIS 

	2) Add a install.nsi file and have it copy to the output filder
		(Copy the contents form another project and modify)

	3) Add build steps:
		PostBuild: 
			cd $(TargetDir)
			if EXIST TalismanSetup.exe del TalismanSetup.exe
			"$(ProjectDir)..\..\packages\NSIS.2.51\tools\makensis.exe" install.nsi


Steps to publish a new version:

	- Update app version
		- Edit assembly version in Project Properties / Application / Assembly Information
		- Edit install.nsi to have the correct version
        - Edit currentVersion.txt to have the correct version
	- Build
	- Upload install exe to share locations

