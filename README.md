# loom-controller


A backend service communicating with Loom-web instance via REST API.<br>
<b>loom-web:</b> https://github.com/DavidSelby/loom-web


 - USB Device recognition
 - Loading tests directly from framework (currently supporting Ruby + Cucumber)
 - Remote command executions for iOS and Android devices
 - Rebooting devices
 - Test reports provider with database
 
# Installation

- Install Visual Studio and build project
- Run command to install ADB via homebrew `brew cask install android-platform-tools`
- Set up and start loom-web project (with mongodb instance named 'arachne')
- Start loom-controller with parameters: 
`ArachneDotNetController.exe {path to your *.feature files} {url to loom-web server}`
