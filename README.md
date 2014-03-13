Excavator
=========

A conversion tool to pull data into Rock from other church management systems.  An auto-generated FellowshipOne model is provided (in-progress).  

Files you need to start an import (Excavator\bin):
- Excavator.exe
- Excavator.exe.config (currently holds the Rock connection string)
- Rock.dll (whichever the latest version is)
- Excavator.F1.dll ( or whichever database component you're using to import)

Note: master branch contains fully-tested code; develop branch is beta/in-progress.

=========
Other Databases:

Additional models can be added by adding a new Class Library and extending the base ExcavatorComponent class.  

Your extension class must implement the TransformData function. 

Export the compiled class (.dll) to the Extensions folder and the app will display the new model on the initial screen.

=========
Fellowship One Conversion Progress:

Completed:
* Individuals
* Families
* Batches
* Contributions
* Pledges
* Addresses
* Companies

Coming Soon:
* Communication
* Accounts
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
