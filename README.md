# AspNetUserSessions
A simple thrad-safe c# class to maintain user sessions in an asp.net or possibly in other IIS-powered webapps.

# Why it was useful for Me
I wanted to get a list of active user (and possibly some that expired not so long ago) sessions in my asp.net webforms app. Theretically, the InProc session cache can be accessed using reflection, but this can be implememtation specific and its thread safety is questionable at best. But the most importantly it was fun to make it.

# How to use it
Include UserSessionList.cs in your project, create an instance of it during application startup and call AddOrUpdate(), UpdateLastActive(), Remove() when appropriate.

The class is thread-safe, modifying the returned lists and objects do not affect the internal lists and objects.

## What to use as SessionID?
Well, anything that uniquely identifies a user session and does not change from request to request. Login name is not very useful, because a user can have multiple sessions using different browsers/incognito tabs from the same computer. 

In my case Session.SessionID was not useful either, because it changed from request to request, and if I start to store something in the Session object (which fixes the volatility of SessionID), locking issues appeared.

At the end I used the XSRF token that gets generated as part of the XSRF protection.

## AddOrUpdate method
This should be called when user logs into the system. Depending on your session management this may or may not be the first one called for a session.
For example if a sessionID is allocated for non-authenticated users, it is good idea to call UpdateLastActive() too for pages that can be accessed anonymously, because it allows us to track non-auth sessions too.

## UpdateLastActive method
Expected to be called by each user request.

## Remove method
Call it when user session ends (e.g.: user logs out of the system)

##GetSessions method
Returns the session list. If the skipCleanExpired if false (or omitted) sessions are removed that has not been active for at least ExpireAfterMinutes.
The objects are copies of the internal objects, modifying them wont affect the contents of the internal list.

##LoadSessions method
This can be used to load previously serialized session list (e.g.: saved into a database or a json file, etc). 
As the list lives in the memory, this is useful when the server restarts (e.g.: get session list using GetSessions() and store them into a database in application_end event, and reload them in Application_Start)

##ExpireAfterMinutes property
default is 30 minutes which is default expiry for forms auth. This property affects which sessions are cleaned in GetSessions. If you'd like to see expired session for a while, set it a little bigger than your forms auth expiry. The list expects that sliding expiration is in effect.
