What's this?
============

These are the setup instructions for working with and compiling Thrive.

Important Note: If you run into any trouble with the setup process, please
bring them up [on the forums](https://community.revolutionarygamesstudio.com/c/dev-help),
or if you are a team member you can ask on the development discord or open a github issue.

If you are a team member you can ask for help on the [Private
Developer
Forums](https://forum.revolutionarygamesstudio.com/c/programming/14)

You can also join and ask on our [community
discord](https://discordapp.com/invite/FZxDQ4H) please use the
#modding or #development channels for that, depending on why you are trying to compile the game
(to make a mod, or to contribute to the development).

Thank you!

Prerequisites
=============

Godot .NET version
------------------

The currently used Godot version is __4.4.1 .NET__. The regular version
will not work. You can download Godot here: https://godotengine.org/download/
if it is still the latest stable version. If a new version of Godot has
been released but Thrive has not been updated yet, you need to look
through the [previous Godot
versions](https://downloads.tuxfamily.org/godotengine/) to get the
right version. Using a different version than what is mentioned above
will cause issues.

Godot is self-contained, meaning that you just extract the downloaded
archive and run the Godot executable in it.


Git with LFS
------------

To clone the Thrive repository properly you need Git with Git
LFS. Note that the GitHub option to download as .zip will not work
(unless that is updated in the future to include LFS assets).

You need at least Git LFS version 2.8.0, old versions do not work.

On Linux use your package manager to install Git. This can be done in
the terminal (if you don't know what is your distro's package manager,
you can look that up with a search engine). On Mac, install the
package manager [homebrew](https://brew.sh/) if you don't already have
it, and use it to install Git. On Mac and Linux Git LFS is likely
available as a package named `git-lfs`. If it is not, install it
manually. You can download Git LFS from
[git-lfs.com](https://git-lfs.com). After downloading the file,
extract it to wherever you want. In the terminal, change directory to
the newly extracted folder (e.g. `cd git-lfs-3.4.1`) and then run the
install script in the terminal with: `sudo ./install.sh`.

After you have installed Git LFS, it must be initialized with the
following command both on Linux and Mac: `git lfs install`. Running
that command should then say "Git LFS initialized." If it doesn't then
something went wrong with either Git or Git LFS installation.

On Windows install Git with the official installer from:
https://git-scm.com/download/win You can use this installer to also
install Git LFS for you. After installing you need to run `git lfs
install` in command prompt. You'll probably want to turn autocrlf on
with the command `git config --global core.autocrlf true`. If you don't,
there is a risk that you accidentally commit Windows-style line
endings.

If `autocrlf` is not used on Windows, then nearly all of the game
files will get marked as changed when Godot editor is opened. So it is
strongly recommended that you follow the configuration instructions
regarding that in the previous paragraph.

If you previously had Git installed through cygwin, you must uninstall
that and install the official Windows version of Git. You may also
have to deleted all your cloned folders to avoid errors, and reboot
your computer to have everything detect that Git is now in a different
place.

If you haven't used Git before you can read a free online book to learn
it here https://git-scm.com/book/en/v2 or if you prefer video learning
these two are recommended https://www.youtube.com/watch?v=SWYqp7iY_Tc
https://www.youtube.com/watch?v=HVsySz-h9r4

.NET SDK
----------

Next you need the .NET SDK. Recommended version currently is 9.0, but a
newer version may also work. You also need *runtime* 8.0 to run Thrive
tests. This can be installed either with the sdk version 8.0 or just
the plain runtime which saves some disk space.

On Linux you can use your package manager to install .NET. The package
might be called `dotnet-sdk-9.0`. For example on Fedora this can be
installed with: `sudo dnf install dotnet-sdk-9.0 dotnet-runtime-8.0`

On Windows you can download the .NET installer from:
https://dotnet.microsoft.com/en-us/download

On mac you can install the dotnet sdk by downloading an installer from
Microsoft's website. Important note for M1 mac users, you need to
install the arm version, the x64 version doesn't work out of the box,
so it is very much not recommended to be used. It is recommended to
use the official mac installer from the .NET website.

The SDK is also available through Homebrew but it will install the
latest version (even if that's not yet officially the version used by
Thrive). But if you want that you can install it using:
```sh
brew install dotnet-sdk
```

At this point you should verify that running `dotnet` in terminal /
command prompt runs the dotnet tool. If it doesn't you don't have .NET
SDK properly installed in PATH. You can list the available SDK
versions you have installed with:
```sh
dotnet --list-sdks
```
The output of that should not be empty.

A development environment
-------------------------

You need a supported development environment for Godot with
mono. Note: it is possible to get by with just C# build tools, but
installing a development environment is the easier route.

Godot currently supports the following development environments:

- Visual Studio 2022
- Visual Studio Code
- JetBrains Rider
- Visual Studio for Mac

### Jetbrains Rider

Jetbrains Rider is recommended for Thrive development on Linux (also
available and a good choice on Windows). It is available from:
https://www.jetbrains.com/rider/

It has a Godot plugin which is easy to install. With Rider the
debugging experience is good.

If building in Rider doesn't work check the build tools used by Rider.

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/rider_godot_plugin.png" alt="rider godot plugin" width="600px">

For better experience make sure to install the Godot plugin for Rider,
which makes debugging the game in Rider easy.

### Visual Studio 2022

On Windows you can use Visual Studio 2022 to work on Thrive. You can
find download and setup instructions here:
https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html#configuring-an-external-editor

### Visual Studio Code

Note: Setting up Visual Studio Code with Linux is possible, however it
is recommended to use Rider instead (this section may be neglected
more than instructions for other tools).

Visual Studio Code, not to be confused with Visual Studio, doesn't
come with build tools, so you'll need to install the build tools for
Visual Studio from here:
https://visualstudio.microsoft.com/downloads/?q=build+tools You will
need **at least** VS Build tools 2022 due to the version of C# used by
Thrive. During the installation process, make sure MSBuild tools is
listed under the installation details.

Note: the following section is not updated for Godot 4

Go back to where you downloaded the .NET SDK from and find _All .NET
Framework Downloads_ Choose version 4.7 and select the Developer Pack.    
If you can't find it you can download the 4.7.2 .NET developer pack from here: https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472

Open Visual Studio Code and go to the Extensions tab. Get the extensions
_C#_, _Mono Debug_, and _C# Tools for Godot_.

Open up a Project in Godot. On the top toolbar, go to Editor -> Editor Settings.
Scroll down on the left window until you find Mono. Click on Editor and set
External Editor to Visual Studio Code. Click on Builds and set Build Tool to
dotnet CLI.

If you want to setup live debugging with Godot follow the instructions here:
https://docs.godotengine.org/en/3.3/getting_started/scripting/c_sharp/c_sharp_basics.html#visual-studio-code


Building Thrive
===============

After downloading all the prerequisites you can now build Thrive.

Clone
-----

If you plan on contributing to Thrive fork it on github so you
have your own repository to work with. There is an in-depth guide
for working with forks
[here](https://gist.github.com/Chaser324/ce0505fbed06b947d962).

Next, use `git clone --recursive` to copy your fork to your
computer. If you're setting up Thrive just for testing or because you
want to try in development features you don't need to fork the
project, and can clone the main Thrive repository. If you cloned
without the `--recursive` flag, you can initialize the submodules
afterwards (`git submodule update --init --recursive`). See the
instructions for that a bit later in this document. Also if you run
into sudden build issues updating the submodules is a good first step
to solving such a problem.

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/terminal_git_clone.png" alt="termina running git clone">

Terminal showing git clone command. If you don't see the line with
"filtering content", then you don't have Git LFS working correctly.
If you don't have Github ssh key setup, you'll want to use a HTTPS URL
for cloning. Note that the screenshot is slightly outdated as it
doesn't clone the submodules with the `--recursive` flag. So use the
up to date command in this document rather than what is shown in the
screenshot.

If you use the "download as zip" option on Github, it won't work. This
is because that option won't properly include the Git LFS files in it.

Thrive uses git submodules so to make sure those are up to date you
need to run the following after cloning or pulling in latest changes
if the submodules were updated: `git submodule update --init --recursive`

Note: a path with spaces in it MAY NOT WORK, so to avoid issues you
should clone to a folder like `~/projects` or `C:/projects`. Also, long
paths may cause issues on Windows. One additional potential problem is
non-English characters in the path name, for example if your path
includes "työpöytä", the setup will fail.

You should check at this point if the image files in
`Thrive/assets/textures` can be opened, if they can't be opened and
their file sizes are tiny, you don't have Git LFS properly installed.

For devs working directly with the Thrive repository switch to a feature
branch or create one when working on new features. For example `git checkout 123_feature`.
This keeps the main branch clean as other branches can be merged through
pull requests on github which is the recommended way to get your code into
Thrive.

Setup
-----

### Compiling C# Code

At this point it is extremely recommended to compile Thrive at least
once before opening it in Godot. This is because otherwise Godot will
unset C# class related properties, leading to various problems.

You can use your IDE or the command line to compile the C#
code. Navigate to the cloned `Thrive` folder and either open the
`.sln` file in your IDE and build, or run `dotnet build` command in
the folder. Both of these options should also restore all NuGet
packages.

If the build fails, check the troubleshooting section "build or script
running fails" later in this document. If the NuGet packages did not
get restored automatically `dotnet restore` can be used to force that
to happen. Once the build succeeds you can proceed to the next
section.

### Project import

Now open your installed Godot version with mono. It should open with a project
manager window. From this window select the option to import a
project.

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/godot_import_button.png" alt="godot import button" width="550px">

Godot project manager with the import button highlighted. If you haven't
used Godot before the list of projects is most likely blank, so please ignore
the demo project in this screenshot.

Use that option and input the path to the folder you cloned
Thrive in and import the project.godot file. Like this:

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/godot_project_import.png" alt="godot project import" width="550px">

Clicking on "import & edit" will immediately open the project in Godot.
So next time you open Godot you will see Thrive on the list of projects,
double click it to open it.

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/project_in_godot_pm.png" alt="godot project manager" width="550px">

Thrive on the project list

Now you should let the Godot editor sit for some time until it is done
checking and importing all the assets. If the asset import fails it is
probably because you didn't have Git LFS installed properly. If that
happens delete the Thrive folder you cloned and go back to the Git LFS
install step, after doing it again try cloning again.

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/thrive_open_in_godot.png" alt="thrive in godot" width="720px">

If everything went fine you should now see Godot editor looking like in this image.

On the top toolbar, go to Editor -> Editor Settings.

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/godot_editor_settings_location.png" alt="godot editor settings">

Scroll down on the left window until you find the Dotnet section (note
the screenshot is from Godot 3, Godot 4 has slightly renamed things).
Click on Editor. Set External Editor to your development
environment. Note that you may need to turn on advanced settings to
see the "Dotnet" settings category.

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/godot_external_editor_settings.png" alt="godot external editor" width="550px">

Here the selected IDE is Rider.

Even if you do not use the Godot script editor, Godot automatically
opens some files and replaces the spaces with tabs.  To stop Godot
from messing with your files, go to Text Editor -> Behavior and set
the "Type" under the "Indent" heading to Spaces.

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/godot_editor_use_spaces.png" alt="set indent to spaces" width="550px">

With Godot 4 it is no longer required to manually switch the build
tool to "dotnet" (the option should no longer exist).

### C# packages

Thrive uses some external C# packages which need to be available for
compiling to work. All IDEs should automatically restore the packages,
but if not `dotnet restore` command can be used manually.

Compiling
---------

Now you should be able to return to the Godot editor and hit the build
button (it is the hammer icon) in the top right corner. If that
succeeds then the C# side of things should be working.

## Native Libraries

Thrive depends on some native libraries which must be present before
the game can be ran.

The easiest way to get these is to download precompiled ones by running:
```sh
dotnet run --project Scripts -- native Fetch Install
```

The debug variants are needed when running through Godot Editor for
Thrive GDExtension to work. The above command installs both variants.

You can compile these libraries locally after installing C++
development tools: cmake, and a compiler. On Linux clang is
recommended (and used by default). Also the build is configured to use
the gold linker which might not be installed by default so that also
needs to be installed (package name is probably something like
`binutils-gold`). On Windows Visual Studio probably works best, but
technically clang should work (please send us a PR if you can tweak it
to work). On Mac Xcode (or at least the command line tools for it)
should be used. The command line tools can be installed with
`xcode-select --install`. Homebrew is again recommended to get the
other tools on mac, like cmake (`brew install cmake`).

For the gdextension to be compiled, `godot` must be available in PATH
to generate the required binding files. And when compiling outside the
container Python and other [Godot build
dependencies](https://docs.godotengine.org/en/stable/contributing/development/compiling/index.html#toc-devel-compiling)
are required to be installed as well.


On Linux installing LLVM tools may help working with the native
libraries, the packages for these tools are usually called `llvm clang
lld` and can be installed with your distro's package manager through
the terminal.

To compile inside a container the native libraries:
```sh
dotnet run --project Scripts -- native Package
```

For a non-container, local compile install the native libraries for the Godot Editor
in the Thrive folder with the following script:
```sh
dotnet run --project Scripts -- native Build Install
```

Note that for release making you need the native libraries for all
platforms, which if you can't build, you should download with:
```sh
dotnet run --project Scripts -- native Fetch
```

Once compiled or downloaded, you need to install the libraries for local Godot
Editor use:
```sh
dotnet run --project Scripts -- native Install
```

The script automatically also handles debug versions of the
libraries. If desired to only work on one variant of libraries the
`--debug false` or `--debug true` parameter can be defined.

Debug versions offer easier native code development / more robust
error condition checking. And the GDExtension library is required in
debug mode when running Thrive through Godot without exporting the
game.

Note that the way to prepare the libraries for distribution requires
two computers: a computer with Linux (a virtual machine likely works),
and a mac (requires Apple hardware). On Linux with the above packaging
commands the binaries can be made for Linux and Windows. Then on a mac
the binaries for mac can be compiled (it is recommended to have the
oldest stil supported mac OS version to ensure maximum compatibility).
Note that an Apple Silicon Mac is likely required for everythign to
work correctly.


## Using Development Environments

You can also compile from your development environment (and not the
Godot editor) to see warnings and get highlighting of errors in the
source code. However running the game from Visual Studio is a bit
complicated.

If the compile fails with a bunch of `Godot.something` or `Node` not
found, undefined references, you need to compile the game from the
Godot editor to make it setup the correct Godot assembly
references. After that compiling from an external tool should work.

From Rider you can compile the game from the top right menu bar by
selecting the `Player` target (with the dotnet icon, the play option
with the Godot logo is for running Godot 3 projects, which Thrive is
no longer). That target should automatically appear once you install
the Godot plugin
(https://plugins.jetbrains.com/plugin/13882-godot-support). If things
don't work you should check Rider settings to make sure that the used
msbuild version is from dotnet and not mono. The first time trying to
use that run target, you need to adjust the path to Godot editor
executable in the configuration edit menu in Rider.

With that plugin you can run the game through Godot (once you have ran once
from Godot editor so that it sets up things), using these toolbar buttons
and options:

<img src="https://randomthrivefiles.b-cdn.net/setup_instructions/images/rider_debugging_buttons.png" alt="rider debug toolbar">

Note: screenshot is for Godot 3, Godot 4 is slightly different.

If it doesn't automatically appear you should be able to manually add it with the
configuration editor.

Done
----

If the build in Godot editor succeeded and the native library install
step succeeded without errors, you should be now good to go.

You can run the game by pressing the play button in the top right of
the Godot editor or by pressing F5. Additionally if you open different
scenes in the editor you can directly run the current scene to skip
the main menu.

If it didn't work you can try these:

- The troubleshooting tips at the end of this document
- Thrive community forums
- Thrive developer forums
- Thrive community discord
- Thrive developer discord (you can only access this if you are a team member)

to get help. Links to the forums and community discord
are at the top of this document.

Git LFS limitations
-------------------

Due to current [Git LFS server](https://github.com/Revolutionary-Games/ThriveDevCenter)
limitations you can't commit changes to files tracked by Git LFS if
you are not a team member. If that is needed for a PR please ask some
team member to commit the assets for you.

For information on committing to the LFS repository you can read this
wiki page https://wiki.revolutionarygamesstudio.com/wiki/Git_LFS

Optional downloads
------------------

In addition to the following optional downloads you need to have Godot
in your PATH for the scripts to find it. To do this create a link /
rename the Godot editor executable to just `godot` or `godot.exe` if
you are on Windows. Then you need to either add the folder where that
executable is to your system PATH or move the executable (along with
the other Godot resources it needs, i.e. the entire `GodotSharp`
folder) to a path that is already in PATH.

### 7zip and zip

7zip is needed for the game release script, so if you're not packaging
the game for release you don't need this.

On Linux and mac, use your package manager to install, it is probably
named `p7zip`.

On Windows download the [official](http://www.7-zip.org/download.html)
installer release. After installing, add the installed folder (where
`7z.exe` is) to PATH environment variable. Confirm by running `7z.exe`
in command prompt or powershell, it should print 7zip version info and
command line usage.

`zip` command is also needed. It is most likely already installed on
Linux and Mac, but needs to be separately installed on Windows.

Linter
------

Thrive uses automatic formatting of source code to keep style as
consistent as possible. It is highly recommended you install
this linter to check your code formatting before submitting a
pull request.

## Jetbrains tools

Jetbrains tools are now installed with dotnet. This happens
automatically as long as you have installed the dotnet sdk.

If you want, you can manually install them with:
```sh
dotnet tool restore
```

If you specify to the formatting check script to not automatically
restore the tools, you'll need to re-run that command in the Thrive
folder whenever our checking tools versions change.

## Localization tools

If you are planning to do anything that would require translation, or
simply to translate the game into your locale language you may need
a few more tools.

If you just want to easily make translations, you can find
instructions for how to do that online in this
[document](working_with_translations.md).

**NOTE: if you are simply planning to edit or add a new localization, Poedit is
enough. You can find more information on how to translate the game with poedit
 [here](working_with_translations.md#Translating-the-game-into-a-new-language).**

### Poedit (optional)

[Poedit](https://poedit.net/) is a free .pot and .po file editor that may
make your life easier when working on translation files.

It is needed to create new .po files without using the command line tools.

NOTE: Poedit will complain about translation format since it was made to
directly use text as keys. Those can be ignored.

### Gettext tools

If you want to run the translation scripts and checks, you need the gettext command 
line tools. They are also an alternative to using Poedit, with the gettext tools you can just
use them and a plain text editor to work on translations.

On Windows you can download precompiled versions of the tools. You
will likely need to extract them and then add the folder you extracted
them in to your PATH for them to be found. Note that there are some
really old versions floating around on the internet. You should try to
find at least version `0.21`.

On Linux use your package manager to install the `gettext` package. On Mac the same package
is available through Homebrew.

`msgmerge` is part of the gettext tools. So any errors with that
missing are about these gettext tools.

## Running the Format Checks

When you are getting ready to commit you should run `dotnet run
--project Scripts -- check` in order to automatically run all of the
formatting tools. Make sure that that script doesn't report any errors
before committing.

When running the script like that it can take a long time to run. See
the pre-commit hook section for how to speed things up.

Alternatively you can run the script `dotnet run --project Scripts --
changes` each time before you run the formatting check. That script
will build a list of changed files that the formatting checks will use
to skip checking non-changed files. But the pre-commit hook is
recommended as it is easier to use.

Pre-commit hook
---------------

You can enable a pre-commit hook to automatically run some of the
formatting checks before each commit to avoid accidentally committing
code with formatting issues. The reason these don't run all of the
checks for each commit is that that takes a really long time. Though,
if you prefer that you can edit the `.pre-commit-config.yaml` file to
switch out which variant of the formatting script is commented out.

To install pre-commit run `pip install pre-commit`. On Linux you can
optionally install it with `sudo` or with the `--user` flag as was
done for the dependencies needed for working with translations. More
instruction for installing pre-commit can be found
[here](https://pre-commit.com/#installation).

Then, to install the hook run the following in the Thrive source
folder:

```sh
pre-commit install
```

The hook has the advantage that it will only run the checks on the
files staged for commit, saving many minutes of time. You can manually
emulate this by creating a file in the Thrive folder called
`files_to_check.txt` with one relative path per line specifying which
files to check.

## Additional Tips

### Build or script running fails (or dotnet restore fails)

First make sure your submodules are up to date:
```sh
git submodule update --init --recursive
```

because if they are not you will have missing or outdated files. Once
you have done that check the other troubleshooting tips if that didn't
help.

If you especially get errors for `RevolutionaryGamesCommon` (for
example
`RevolutionaryGamesCommon\DevCenterCommunication\DevCenterCommunication.csproj`
or other csproj or .cs files missing), first try initializing and
updating git submodules and building again.

If you get errors from any files in the Scripts folder, for example
`Thrive\Scripts\Program.cs`, then you likely have an outdated version
of the submodules. Running the above submodule update command should
fix these kind of errors as well.

### The project file was not found

If you get errors like "The project file ... ScriptsBase.csproj was
not found", then that is probably caused by git submodules not being
initialized or being not up to date. Run `git submodule update --init`
to fix the issue in the cloned folder on your computer. 

Also if you get any other error when trying to restore the nuget
packages, it might also be caused by the submodules. And the third way
to get this error is likely when trying to run the scripts in the
repository without again having the submodules be up to date.

### Godot asset import fails / images can't be opened

The most likely case with Godot not being able to import the binary
assets and none of the game images being able to be opened with an
image viewer, is that the Thrive repository was not cloned with the
LFS data. Please see above the LFS instructions and try doing it again
with those instructions until the downloaded image files can be
opened. After that Godot should be able to import all the assets
properly.

### Native build issues on Mac

First make sure that command line tools are installed with
`xcode-select --install` and then make sure homebrew packages are
updated: `brew upgrade`.

If the build still doesn't work, double check that all correct
packages are installed and homebrew didn't report any caveats with
PATH or something like that that would prevent the tools from being
found.

### Troubleshooting regarding Godot automatically breaking

Godot sometimes likes to break your files for no reason. If you keep
the Godot editor open while pulling new changes or changing branches,
it's very likely to break so it is recommended to close Godot when
doing such operations that change files outside the Godot editor, and
then reopening the editor afterwards.

In Godot 4 also hot reload sometimes stops working, at this time the
only thing to do is to close Godot and reopen it. Note that any scenes
saved while in this broken state will destroy all C# properties and
signal connections.

Because Godot sometimes just breaks files, before reporting an issue
building the game please check that `git status` returns no
changes. If there are changes reported that you didn't make manually,
then see the section below about cleaning Godot.

### Build problems with unsupported C# version

If the build fails with errors about unsupported C# language version,
you need to update your VS build tools, if using that, or .NET SDK if
using another IDE.

### Cleaning Godot

Your locally cloned Thrive version may get messed up from time to time.
[Here are the steps to fix it.](https://wiki.revolutionarygamesstudio.com/wiki/Cleaning_Local_Thrive_Version)

### Translating the game

You can find information about how to translate the game on the 
[Working with translation page](working_with_translations.md).

### Errors with running the translation extraction (`Failed to find translation tool 'msgmerge'`)

These errors are related to the gettext tools, which must be installed
and added to system PATH. So if you get this error double check that
you have installed latest gettext version (some Windows sources for
these tools have really old versions) and added the folder to
PATH. After installing you need to restart the terminal / command
prompt you are using to run the translation extraction as it will
still be using the old PATH value. The easiest solution to that is to
restart the terminal / command prompt so that it loads the new PATH.

### All files are marked as changed

If you are on Windows and you see that most game files are marked as
changed after opening the Godot editor, then check your `autocrlf`
setting (instructions are in this file), and reclone or recheckout all
of the game files (while Godot is closed).

### Code checks (inspectcode, cleanupcode) report incorrect errors

Due to a problem with caching with those tools, they may not be using
the latest configuration on your computer. To fix this problem (for
now, this has been [reported to
jetbrains](https://youtrack.jetbrains.com/issue/RSRP-484743) you need
to delete some cache folders.

On Linux these folders are:
```
~/.local/share/JetBrains/Transient
~/.local/share/JetBrains/InspectCode
/tmp/JetBrainsPerUserTemp-*
```

On Windows the cache folders are somewhere in your APPDATA folders.

### Export issues on Mac (or .NET Assemblies not found popup)

As Godot is installed as an .app a symbolic link to the actual Godot
executable within it needs to be created in the `~/bin` folder (and
also the shell profile needs to be updated to make sure that the user
bin folder is in PATH). Also the .NET resources need a symbolic link
to work.

For example:
```sh
cd ~/bin
ln -s /Applications/Godot_mono.app/Contents/MacOS/Godot godot
ln -s /Applications/Godot_mono.app/Contents/Resources/GodotSharp GodotSharp
```

Then restart the terminal / reboot to get the new PATH to definitely
apply.

## Exporting the game

### Prerequisites

There is a provided script `dotnet run --project Scripts -- package`
which helps with bundling the game up for releases. This relies on
`godot` (or `godot.exe`) being the name of the Godot editor that is
the current version and it being in PATH.

To set this up basically create a new folder that you add to PATH (Windows 
registry, `.bashrc` or `.zshrc` for Linux/Mac) and create a copy or 
symbolic link in it named `godot`. 

For Mac if you copied the Godot editor to your apps folder, like you should,
run the following (and then edit `.zshrc`):
```sh
mkdir ~/bin
cd ~/bin
ln -s /Applications/Godot_mono.app/Contents/MacOS/Godot godot
./godot
```
The last command should have opened the Godot editor correctly. If it did,
close it and you are now ready to edit your shell startup script to put
the bin folder in your PATH. You can run `pwd` command to see the full
path you need to include there.

### Running the script

After you have installed the prerequisites and checked the game runs fine
from the Godot editor, you can just run the export script:
```sh
dotnet run --project Scripts -- package
```

Or if you want more control you can select which platforms to export to
and skip zipping up the folder if you just want to test locally:
```sh
dotnet run --project Scripts -- package -z false Windows
```

For more options run the script with the `-h` parameter to see all of them:
```sh
dotnet run --project Scripts -- package --help
```
