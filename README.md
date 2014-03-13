Excavator
=========

A conversion app to import data into Rock from other church management systems.  This app requires the SQL file (.mdf) from your previous database and an active Rock connection.

An auto-generated FellowshipOne model is provided (in-progress).

Files you need to start an import (Excavator\bin):
- Excavator.exe
- Excavator.exe.config (currently holds the Rock connection string)
- Rock.dll (whichever the latest version is)
- Excavator.F1.dll ( or whichever database component you're using to import)

Note: master branch contains fully-tested code; develop branch is beta/in-progress.

=========
Adding other database models:

1. Download the entire Excavator solution from GitHub 
2. Add a new Class Library that extends the base ExcavatorComponent class
3. Add references to all the packages inside Excavator\Packages and set their "Copy Local" property to false. 
These packages are embedded in the Excavator app; if you have additional references set "Copy Local" to true.
4. Implement the TransformData function inside your ExcavatorComponent
5. Build or copy the compiled library (.dll) to Excavator\bin or Excavator\Extensions
6. Run Excavator and your new database model should be in the list on the first page

=========
Fellowship One Conversion Progress:

Completed:
* Individuals 
* Families (excluding visitors)
* Batches
* Contributions
* Pledges
* Addresses
* Companies
* Accounts

Coming Soon:
* Communication
* Notes

Inconsistent/Not Supported:
* Attendance
* Campuses
* RLC (Room, Location, Class)
* Ministries
* Assignments

=========
Licensed under the Apache License, Version 2.0. You may not use this application except in compliance with the License.

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and
limitations under the License.
