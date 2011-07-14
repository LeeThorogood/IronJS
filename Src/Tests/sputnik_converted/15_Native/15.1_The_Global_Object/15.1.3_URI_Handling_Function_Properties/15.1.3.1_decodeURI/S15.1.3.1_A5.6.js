// Copyright 2009 the Sputnik authors.  All rights reserved.
// This code is governed by the BSD license found in the LICENSE file.

/**
* @name: S15.1.3.1_A5.6;
* @section: 15.1.3.1;
* @assertion: The decodeURI property has not prototype property;
* @description: Checking decodeURI.prototype;
*/


// Converted for Test262 from original Sputnik source

ES5Harness.registerTest( {
id: "S15.1.3.1_A5.6",

path: "TestCases/15_Native/15.1_The_Global_Object/15.1.3_URI_Handling_Function_Properties/15.1.3.1_decodeURI/S15.1.3.1_A5.6.js",

assertion: "The decodeURI property has not prototype property",

description: "Checking decodeURI.prototype",

test: function testcase() {
   //CHECK#1
if (decodeURI.prototype !== undefined) {
  $ERROR('#1: decodeURI.prototype === undefined. Actual: ' + (decodeURI.prototype));
}

 }
});
