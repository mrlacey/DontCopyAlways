# Don't Copy Always

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
![Works with Visual Studio 2019](https://img.shields.io/static/v1.svg?label=VS&message=2019&color=5F2E96)
![Works with Visual Studio 2022](https://img.shields.io/static/v1.svg?label=VS&message=2022&color=5F2E96)

[![Build](https://github.com/mrlacey/DontCopyAlways/actions/workflows/build.yaml/badge.svg)](https://github.com/mrlacey/DontCopyAlways/actions/workflows/build.yaml)

Download the extension from the [VS Marketplace](https://marketplace.visualstudio.com/items?itemName=MattLaceyLtd.DontCopyAlways)

-------------------------------------

Visual Studio extension that checks for files that have 'Copy to output directory' set to 'Copy always'.

See [this explanation of why this matters](./explanation.md).

Any affected files will be listed in the Output Pane.

A context menu entry on projects (in the Solution Explorer) allows you to "fix" all affected items in that project.
