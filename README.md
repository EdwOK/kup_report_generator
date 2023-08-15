# KUP Report generator

[![build](https://github.com/EdwOK/kup_report_generator/actions/workflows/build.yml/badge.svg)](https://github.com/EdwOK/kup_report_generator/actions/workflows/build.yml)
[![release](https://github.com/EdwOK/kup_report_generator/actions/workflows/release.yml/badge.svg?event=release)](https://github.com/EdwOK/kup_report_generator/actions/workflows/release.yml)

Simple console tool to generate  [KUP reports](https://www.pit.pl/koszty-uzyskania-przychodu-pit/) based on git commits history in Azure DevOps.

## Prerequisites

Before using the **KUP Report generator**, ensure that you have the following prerequisites in place:
- [.NET SDK](https://dotnet.microsoft.com/en-us/download/visual-studio-sdks) installed (version 5.0 or later)
- [Git Credential Manager](https://github.com/git-ecosystem/git-credential-manager) installed

### Obtaining git credentials for Azure DevOps

To access the git commit history from Azure DevOps, you need to have the appropriate git credentials. Follow these steps to obtain them:

#### Windows

1. Open the Credential Manager on Windows by pressing `Win + R` to open the Run dialog, then type `control /name Microsoft.CredentialManager` and press Enter.
2. In the Credential Manager window, click on the _"Windows Credentials"_ tab.
3. Scroll down and look for any entries related to the Azure DevOps. These entries may start with `git:https://`, `git:https://dev.azure.com/`, or the name of your dev.azure.com organization.
4. Click on the entry to expand it and view the stored credentials. You should see the username and the option to reveal the password.

#### Linux

1. Open a terminal.
2. Run the following command to check the stored Git credentials: `git credential fill`.

#### MacOS

1. Open a terminal.
2. Run the following command to check the stored Git credentials: `git credential-osxkeychain get`.

## Installation 

1. Download the latest version of the **KUP Report generator** for your OS:
- [Win x64](https://github.com/EdwOK/kup_report_generator/releases/latest/download/kup_report_generator_win_x64.zip)
- [Linux x64](https://github.com/EdwOK/kup_report_generator/releases/latest/download/kup_report_generator_linux_x64.tar.gz)
- [MacOS x64](https://github.com/EdwOK/kup_report_generator/releases/latest/download/kup_report_generator_macos_x64.zip)
2. Launch and select `Install` option and  follow the steps.
3. Launch and select `Run` and wait for the result.
