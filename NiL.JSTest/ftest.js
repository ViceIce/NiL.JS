﻿function __func() { };

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (!(Function.prototype.isPrototypeOf(__func))) {
    $ERROR('#1: Function.prototype.isPrototypeOf(__func)');
}
//
//////////////////////////////////////////////////////////////////////////////


var __gunc = function () { };

//////////////////////////////////////////////////////////////////////////////
//CHECK#2
if (!(Function.prototype.isPrototypeOf(__gunc))) {
    $ERROR('#1: Function.prototype.isPrototypeOf(__gunc)');
}