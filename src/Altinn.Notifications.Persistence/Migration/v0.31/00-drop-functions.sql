drop function if exists getemailrecipients(_alternateid uuid);
drop function if exists getemailsummary(_alternateorderid uuid, _creatorname text);
drop function if exists getsmsrecipients(_orderid uuid);
drop function if exists getsmssummary(_alternateorderid uuid, _creatorname text);