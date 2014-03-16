Excavator
=========

A conversion app to import data into Rock from other church management systems.  This app requires the SQL file (.mdf) from your previous database and an active Rock connection.

A FellowshipOne model is provided (in-progress).

Files you need to start an import (Excavator\bin):
- Excavator.exe
- Excavator.exe.config (currently holds the Rock connection string)
- Rock.dll (whichever the latest version is)
- Excavator.F1.dll ( or whichever database componenet you're using)

Note: master branch contains fully-tested code; develop branch is beta/in-progress.

=========
Adding other database models:

Simple version:
1. Download the zipped copy of Excavator from GitHub and rewrite Excavator.Example
2. Build or copy the compiled library (Excavator.Example.dll) to Excavator\bin or Excavator\Extensions
3. Run Excavator and select your new database component from the list on the first page

Advanced:
1. Download the zipped copy of Excavator from GitHub 
2. Add a new Class Library that extends the base ExcavatorComponent class
3. Add a reference to Excavator (from Solutions\Projects)
4. Add references to all the packages inside Excavator\Packages and set their "Copy Local" property to false.  See note below.  
5. Set the FullName of your specific database model 
6. Implement the TransformData method inside your new ExcavatorComponent
7. Build or copy the compiled library (.dll) to Excavator\bin or Excavator\Extensions
8. Run Excavator and select your new database component from the list on the first page

**Note: These packages are needed for Rock but are embedded in the Excavator app.  If you use additional references, set "Copy Local" to true.

=========
Fellowship One Conversion Notes:

Completed:
* Individuals 
* Families (excluding visitors)
* Batches
* Contributions
* Pledges
* Addresses
* Companies
* Accounts
* Communication

Coming Soon:
* Notes

Inconsistent/Not Supported*:
* Attendance
* Campuses
* RLC (Room, Location, Class)
* Ministries
* Assignments

** These tables depend on the specific structure of your church's ministries and groups.  Given enough sample data, it's possible generic import maps could be added.  Otherwise these import maps are custom to each church.

*** If you are a multi-site church and have contributions tied to campuses, enter each campus in Rock before starting the import.  The Pledge & Contribution maps look for an exact match on the name or shortcode to assign a specific campus.

=========
Licensed under the Apache License, Version 2.0. You may not use this application except in compliance with the License.

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.
