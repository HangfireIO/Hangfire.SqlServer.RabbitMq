Hangfire.SqlServer.RabbitMq
============================

[![Maintainers Wanted](https://img.shields.io/badge/maintainers-wanted-red.svg)](https://github.com/pickhardt/maintainers-wanted) [![NuGet](https://img.shields.io/nuget/v/Nuget.Core.svg)](https://www.nuget.org/packages/Hangfire.SqlServer.RabbitMQ/) [![Build status](https://ci.appveyor.com/api/projects/status/syxk488k2e45v2fo?svg=true)](https://ci.appveyor.com/project/odinserj/hangfire-sqlserver-rabbitmq)

RabbitMQ queues support for SQL Server job storage implementation for Hangfire.

Building the sources
---------------------

To build a solution and get assembly files, just run the following command. All build artifacts, including `*.pdb` files, will be placed into the `build` folder. **Before proposing a pull request, please use this command to ensure everything is ok.** Btw, you can execute this command from the Package Manager Console window.

```
build
```

To build NuGet packages as well as an archive file, use the `pack` command as shown below. You can find the result files in the `build` folder.

```
build pack
```

To see the full list of avalable commands, pass the `-docs` switch:

```
build -docs
```

Hangfire uses [psake](https://github.com/psake/psake) build automation tool. All psake tasks and functions defined in `psake-build.ps1` (for this project) and `psake-common.ps1` (for other Hangfire projects) files. Thanks to the psake project, they are very simple to use and modify!

License
--------

The MIT License (MIT)

Copyright (c) 2016 Denny Ferrassoli

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
