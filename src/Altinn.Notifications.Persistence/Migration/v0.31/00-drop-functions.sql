drop function if exists notifications.getemailrecipients(_alternateid uuid);
drop function if exists notifications.getemailsummary(_alternateorderid uuid, _creatorname text);
drop function if exists notifications.getsmsrecipients(_orderid uuid);
drop function if exists notifications.getsmssummary(_alternateorderid uuid, _creatorname text);

drop procedure if exists notifications.insertemailnotification(IN _orderid uuid, IN _alternateid uuid, IN _toaddress text, IN _result text, IN _resulttime timestamp with time zone, IN _expirytime timestamp with time zone);
drop procedure if exists notifications.insertsmsnotification(IN _orderid uuid, IN _alternateid uuid, IN _mobilenumber text, IN _result text, IN _resulttime timestamp with time zone, IN _expirytime timestamp with time zone);
drop procedure if exists notifications.insertsmsnotification(IN _orderid uuid, IN _alternateid uuid, IN _recipientorgno text, IN _recipientnin text, IN _mobilenumber text, IN _result text, IN _resulttime timestamp with time zone, IN _expirytime timestamp with time zone);
