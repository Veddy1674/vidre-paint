<img width="202" height="83" alt="image" src="https://github.com/user-attachments/assets/fa007456-0df0-401e-9e1c-9d577d4887c0" />  

A cross-platform lightweight and optimized **raster** painting software built in C# as an alternative to Paint.NET *(which is only meant for Windows)*

![Progress](https://img.shields.io/badge/progress-20%25-orange)

---

This is a personal and as of now **unfinished** software that I developed when I moved from Windows to Linux and couldn't use Paint.NET, and the alternatives did not satisfy my needs.  

Here's a screenshot of the software:

<img width="1920" height="1038" alt="Preview" src="https://github.com/user-attachments/assets/abdf649b-90a8-454f-b5d5-569644609c54" />
<sub>The depicted image in the screenshot was published by <a href="https://unsplash.com/photos/a-body-of-water-surrounded-by-mountains-and-trees-eXuWi5jvXN0">Leonid Privalov</a></sub>

## Details:

This software was developed in C# (.NET 10) with Silk.NET and SkiaSharp, and was optimized mainly to minimize startup time and RAM usage.

As of now it includes most of the features found commonly in Raster painting software, but as it is incomplete, it still lacks some fundamental features for it to be able to fully replace any other painting software.

I plan to keep developing it at a slow pace (I develop features only when I strictly need them) and eventually it will be a complete software.

Along with standard painting software features, I plan on adding specific tools meant for Machine Learning, and perhaps a plugin system to allow custom tools and effects to be created.

## Info about usage:

The first argument of the program can be the path of an image file to open on startup.  
Thanks to this, you can set Vidre as your default image viewer, and as it is optimized for fast startups, it's a great way to quickly view images.

When you run the software for the first time, a configuration file is created (Managed in [`Config.cs`](./src/Config.cs)) for future personalization purposes, alternatively the argument `--noconf` disables loading/saving for the session (thus temporarily runs the software with default settings).
