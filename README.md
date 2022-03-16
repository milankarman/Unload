<br />
<p align="center">
  <img src="Unload/icon/icon.png" alt="Logo" width="60" height="60">

  <h2 align="center">Unload</h2>

  <p align="center">
    <i>An automatic speedrun load time remover for community verifiers.</i>
    <br />
    <a href="../../issues">Report Issue</a>
    -
    <a href="../../issues">Request Feature</a>
      -
    <a href="https://www.youtube.com/watch?v=tgFAWxCUGZY">Video Guide</a>
  </p>
</p>

# Table of Contents

* [About](#about)
* [Features](#features)
* [Usage](#usage)
* [Download](#download)
* [Will my game work with Unload?](#will-my-game-work-with-unload)
* [Extra Info](#extra-info)
* [Credits](#credits)


# About

Speedrun timing is a tricky subject. A community doesn't want to give anyone an unfair advantage but you also don't want the timing and verification process of new runs to take too long. Unload is made to help with those speedruns that choose loadless timing yet require a lot of verification work. I ran into this problem with speedruns of the game "The Hobbit" which I verify - and thus Unload was born.

If the game you verify has consistent recognizable loading screens then Unload should be able to detect them all and give accurate loadless timing.

**Unsure if your speedrun videos will work with Unload? [Read this!](#will-my-game-work-with-unload)**

<div align="center">
  <img src="img/banner.png" alt="Triangulation view" width="100%" height="auto">
</div>
<br />

# Features
* Pick one or more loading screens and detect them everywhere.
* Loads are picked from the video you're verifying, so even bad video quality load screens can be recognized.
* Quickly check and adjust load frames with the dedicated load checking window.
* Unload can work with most video formats.
* Option to export framecounts to keep a clean log of verifications.
* Options to crop and change VODs for fast processing

# Usage

**If you would rather watch a video guide then go [here](https://www.youtube.com/watch?v=tgFAWxCUGZY).** Note that this is slightly outdated by now but the workflow remains mostly the same.

1. When starting Unload you'll be welcomed with the start menu. Here choose "Convert" and pick the video you'd like to frame count.
2. You'll be met with the conversion window, here you can choose to cut to the part of the video where the run actually takes place. If the game you're is locked at a lower framerate than the video then you want to change the FPS setting to match that (Example: a game runs at 30fps while the video is recorded at 60fps). Hit convert when you're happy with your settings.
3. After converting you'll be met with the main window. Find the frame where the speedrun starts using the navigation down at the bottom, then hit "Set Start". Do the same for the frame where the speedrun ends (the last frame of gameplay) and hit "Set End".
4. Now use the video navigation to find a loading frame, then hit the "+" button in the load picking group in the top left. You should see your load screen show up in that group.
5. Then we want to crop to the distinct part of the loading screen, this will differ from game to game but the more distinct the better. You can click to draw a rectangle over the load screen preview to crop quickly, or by adjusting the sliders in the load picking sections (Drawing your cropping is only possible when the sliders aren't changed).
6. Once you're happy with that we can go over to the top right load detection group and hit "prepare frames", note that this might take a bit of time and needs to be re-done if you want to change your cropping, so ensure that your cropping is correct.
7. Now you're ready to hit "detect load frames" in the load detection group. You should then see a list of detected load frames as well as the calculated times in the bottom left frame count group.

Now you can export your frame count to a file using the "export times" button, however it's recommended that you double check the detected loads using the steps below.

8. Hit "open load checking window" below the detected load list. You'll be met with a window showing the start frame of a loading screen, ending frame of a loading screen and the frames surrounding those.
9. Use the load navigation buttons and slider to check if there are any incorrect loads or missing loads.
10. If you notice missing loads, return to the main menu and try lowering the minimum similarity and hitting "detect load frames again". If you notice a lot of false positives try raising the minimum similarity and detecting load frames again.
11. Finally you can adjust slight mistakes using the load check window, I recommend just playing around with this.

And that's all you need to get started. There are more options for more specific use cases which aren't described here that you can feel free to mess around with.

# Download

You will need to have the [.NET 5.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-5.0.406-windows-x64-installer) installed. Download the most recent release [here](../../releases). Download the .zip file and extract the full "Unload" folder into the location where you want to keep it. Then you can run the Unload.exe file inside and you're up and running!

Unload is only available for Windows, there hasn't been demand to port it and it will likely take a large amount of effort..

# Will my game work with Unload?

To answer this you'll have to look at how your game looks when it loads:

- If your loading screens contain a logo, image or anything that's distinct on every load screen then there's a great chance Unload can detect it.

- If you have a set of different looking loading screens and all are clearly different from gameplay then there's a great chance you can choose them all and Unload can detect them.

- If your loading screens are pure black screens, Unload *might* work but you would have to be careful that no non-loading black screens get picked up.

Note that loading screens that fade in or out would require some extra adjustment to get the timing just right, but they will likely work if they match what's written above.

And if you're still not sure there's never harm in trying.


<p align="center">
  <img src="img/load_example.png" alt="Loading screen example" width="100%" height="auto">
      <i>An easy to detect load screen in the game "The Hobbit."</i>
  <br />
</p>

# Extra Info

Here I'll answer some things you might be wondering in a questions-and-answers format.

**How does Unload work behind the scenes?**  

*Unload calculates something callled a Perceptual Hash (PHash) for every video frame as well as the picked loading frames. Using this PHash it can compare how similar the video frames are to the picked loading frames and if they are similar enough they can be confidently counted as load frames. From there it's simple math to subtract the load frames from real time.*

**How can I speed up conversion?**  

*The best way to go about speeding up conversion is to work with less video data. You can set the start and end time to be closer to the start and end of the run where applicable to avoid converting as many frames.  
You might also be able to lower the framerate if, for example, the game has a locked framerate at 30 FPS but the video is recorded at 60.  
Last but not least you can lower the export width and height, but know that this won't speed up conversion a lot and if the quality gets too low then loading frames might be harder to recognize.*

**Why does every loading screen need the same cropping?**  

*This is because Unload applies the same loading frame cropping to every video frame before PHashing it. If there are multiple different croppings that would mean ***all*** video frames would have to be PHashed multiple times, multiplying processing time for every new load frame.*

**How can I speed up frame preperation?**  

*Frame preparation is very dependent on the size of your loading screen cropping. If you hardly crop at all, then the process will likely take a long time. Thus try to find something smaller yet distinct on your loading screens to be recognized and crop to that when possible.*

**Does Unload support alpha masks for loading frames?**  

*No, this is not supported yet. But it's something I would like to add eventually.*

**Can unload work on live runs together with my timer?**  

*No, Unload is purely designed for verification purposes.*


**What does it mean when Unload tells me "Fewer converted frames are found than expected"?**  

*When Unload loads in your video file it checks how many frames it expects there to be in the video. It then checks how many frames it finds in your converted "_frames" folder. If this is less than it expects then there's a chance some frames might be missing, possibly due to a conversion error. The warning is just to make you aware of this, as there's a chance that might lead to innacurate load time calculation.*

# Credits

- [Shockster](https://github.com/Shockster218): Support and feedback
- [Avasam](https://github.com/Avasam): Feedback and code contributions
- [SteveManaclaw](https://github.com/SteveManaclaw): Feedback
- [Xabe.FFMpeg Library](https://ffmpeg.xabe.net/index.html): FFMpeg Library
- [Shipwreck.Phash](https://github.com/pgrho/phash): Phash Library
