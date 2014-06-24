<b>Excavator</b>
=========

Excavator converts data into [Rock RMS](http://www.rockrms.com/) from other church management systems.  You will need the SQL file (.mdf) from your previous database and an active Rock connection.

Imports from FellowshipOne and Arena ChMS are in active development, as well as an option to import from Excel/CSV.  Currently only the FellowshipOne model has been released.

Files you need to start an import (Excavator\bin):
- Excavator.exe
- Rock.dll (whichever the latest version is)
- Excavator.exe.config (currently holds the Rock connection string)
- Excavator.F1.dll ( or whichever database component you're using)

Note: master branch contains fully-tested code; develop branch is beta/in-progress.

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
<b>Fellowship One Conversion Notes:</b>

Completed:
* Accounts
* Addresses
* Batches
* Communication
* Companies
* Contributions
* Families (excluding visitors)
* Individuals 
* Pledges
* Notes
* Users

Coming Soon:
* Requirements

Inconsistent/Not Supported**:
* Assignments
* Attendance
* Attributes
* Campuses***
* Groups
* Ministries
* RLC (Room, Location, Class)

** These tables depend on the specific structure of your church's ministries and groups.  Given enough sample data, it's possible generic import maps could be added.  Otherwise these import maps are custom to each church.

*** If you are a multi-site church and have contributions tied to campuses, enter each campus in Rock before starting the import.  The Pledge & Contribution maps look for an exact match on the name or shortcode to assign campuses.

=========
Licensed under the Apache License, Version 2.0. You may not use this application except in compliance with the License.

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.
