# Why you shouldn't "Copy always"

When the "Copy to output directory" property of a file is set to `Copy always` the compiler will always rebuild the project so that the most up to date copy of that file is available.
This applies even if nothing in the code has changed.

Rebuilding your projects when you don't need to can take time (which costs money) and interupt your flow. Both of which can lower your productivity.

If you don't need a file to be copied to the output directory set this property to `Do not copy`.

If you do need the file copied to the output directory but don't want the project ot be rebuilt if nothing has changed, set this property to `Copy if newer`.
