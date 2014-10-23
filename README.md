**Welcome to Excavator!**

Build Status: [![Stories in Ready](https://badge.waffle.io/newspring/excavator.png?label=ready&title=Ready)](https://waffle.io/newspring/excavator)


![](https://raw.githubusercontent.com/wiki/newspring/excavator/excavator.jpg)

Excavator converts data into [Rock RMS](http://www.rockrms.com/) from other church management systems.

Imports from FellowshipOne and CSV are currently supported.  A port for Arena ChMS is in active development.

## What You'll Need To Get Started
- A local or hosted version of Rock
- A login to the Rock SQL database
- Your old database in a supported format
- Excavator files( see below for download )

## Downloads
- [Excavator.zip](https://github.com/NewSpring/Excavator/blob/master/Excavator.zip)

## What If I Have A Problem?
- If you have a problem with the import, please file an issue on Github: [Excavator Issues](https://github.com/NewSpring/Excavator/issues)
- Please include the type of import (F1 or CSV) in the title and your Windows environment settings in the body.
- Example issue: "CSV: Can't import prefix with special characters"

Please note that the master branch contains fully-tested code; develop branch is beta/in-progress.

=========
<b>Extending/Adding other database models:</b>

<i>Simple version:</i><br>
1.  Download the zipped copy of Excavator from GitHub and rewrite the Excavator.Example project<br>
2.  Build or copy the compiled library (Excavator.Example.dll) to Excavator\bin or Excavator\Extensions<br>
3.  Run Excavator and select your new database component from the list on the first page<br>

<i>Advanced version:</i><br>
1.  Download the zipped copy of Excavator from GitHub <br>
2.  Add a new Class Library that extends the base ExcavatorComponent class<br>
3.  Add a reference to Excavator (from Solutions\Projects)<br>
4.  Add references to all the packages inside Excavator\Packages and set their "Copy Local" property to false.*<br>
5.  Set the FullName of your specific database model <br>
6.  Implement the TransformData method inside your new ExcavatorComponent<br>
7.  Build or copy the compiled library (.dll) to Excavator\bin or Excavator\Extensions<br>
8.  Run Excavator and select your new database component from the list on the first page<br>

\* If you use additional references, set "Copy Local" to true.  Copy the additional .dll to your server for Excavator to run.

=========
Licensed under the Apache License, Version 2.0. You may not use this application except in compliance with the License.

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.
