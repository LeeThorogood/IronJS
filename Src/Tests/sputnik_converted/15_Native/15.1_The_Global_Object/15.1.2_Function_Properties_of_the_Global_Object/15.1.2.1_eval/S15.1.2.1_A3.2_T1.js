// Copyright 2009 the Sputnik authors.  All rights reserved.
// This code is governed by the BSD license found in the LICENSE file.

/**
 * @name: S15.1.2.1_A3.2_T1;
 * @section: 15.1.2.1, 12.1;
 * @assertion: If Result(3).type is normal and its completion value is empty, 
 * then return the value undefined; 
 * @description: Block statement;
*/


// Converted for Test262 from original Sputnik source

ES5Harness.registerTest( {
id: "S15.1.2.1_A3.2_T1",

path: "TestCases/15_Native/15.1_The_Global_Object/15.1.2_Function_Properties_of_the_Global_Object/15.1.2.1_eval/S15.1.2.1_A3.2_T1.js",

assertion: "If Result(3).type is normal and its completion value is empty,",

description: "Block statement",

test: function testcase() {
   //CHECK#1
if (eval("{}") !== undefined) {
  $ERROR('#1: eval("{}") === undefined. Actual: ' + (eval("{}")));
}    

 }
});
