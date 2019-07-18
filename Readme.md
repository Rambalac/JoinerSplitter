JoinerSplitter
==============

Another GUI for FFMpeg for fast video joining and cutting.

The main point of the editor is using `-c copy` feature of FFMpeg, in other terms it's called Smart Rendering. 
That allows to process even 4K files faster than videos duration (~300fps in my case). 
You can cut and/or join videos in any combination, but all videos for join must be the same format.
Cuts will be done by closest keyframes, or at lease it will try to do the best.

Requirements
------------
Windows with .NET4.7.2 installed.

Installation
------------
* Install from `msi`. 

Current limitations and issues
------------------------------
Application was tested only with Lumix GH3, Lumix GH4, Lumix GH5 (including 4K) and Mavic 2 PRO MOV videos.
