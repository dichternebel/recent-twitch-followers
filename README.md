# Recent Twitch followers for OBS [![Licensed under the MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/dichternebel/recent-followers-for-obs/blob/main/LICENSE.md)
Get your most recent Twitch followers for creating a follower rotation or displaying their avatars in OBS.

## What is this?
This small console application will gather information from your Twitch account and write it down to a text file.

In addition it grabs your 5 most recent follower names and rotates them every 5 seconds. For those followers, their avatars are also stored locally and are enriched with their Twitch display names.

## What do I get?

Well, something like this:  
![follower rotation animation](assets/follower_rotation.gif)

Please have a look at my [Twitch channel]((https://twitch.tv/dichternebe1)) to see it in action. I am using it to create the intro scene, the follower rotation and outro scene.

[![https://twitch.tv/dichternebe1](https://static-cdn.jtvnw.net/jtv_user_pictures/95c739c7-5731-4966-9c07-9e7884aee938-profile_image-150x150.png)](https://twitch.tv/dichternebe1)

## How does it work?
Simple!
- Download the zip file from the [Releases](https://github.com/dichternebel/recent-followers-for-obs/releases) section.
- Extract and start the executable somewhere e.g. in C:\StreamingTools
- Enter your Twitch username, allow the application to connect to Twitch and that's it.

Keep the application running to get the job done as long as you are streaming or working in OBS.

## What has to be done in OBS?
To get a working followers rotation you only have to add a source called **"Text(GDI+)"** to your scene in OBS and point to the text file called **"currentFollower.txt"** located in the output folder. Optionally add some nice background to it.

To get sources for your avatar followers, just add a **"Media source"** pointing to the corresponding image in the output folder. Your latest follower has avatar1.png and so on.

Enjoy!  
DichterNebel
