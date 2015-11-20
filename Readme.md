JoinerSplitter
==============

Another GUI for FFMpeg for fast video joining and cutting.

The main point of the editor is using -c copy feature of FFMpeg, in other terms it's called Smart Rendering. That allows to process even 4K files faster than videos duration (~300fps in my case). You can cut and/or join videos in any combination, but all videos for join must be the same format.

Installation
------------
* Install from msi. 
* Install ffmpeg.exe and ffprobe.exe into JoinerSplitter folder or add to Path.

Current limitations and issues
------------------------------
Application was tested only with Lumix GH3 and Lumix GH4 (including 4K) MOV videos.
* There is no way to save project. 
* There is no validation if video is not supported by FFMpeg.
