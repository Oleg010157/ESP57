/* =================================================================================
File name:       PI.H 
===================================================================================*/


#ifndef __PI_H__
#define __PI_H__

typedef struct {
                  float  Ref;   			// Input: reference set-point
				  float  Fbk;   			// Input: feedback
				  float  du;             // Input: feedback
				  float  Out;   			// Output: controller output
				  float  Kp;				// Parameter: proportional loop gain
				  float  Ki;			    // Parameter: integral gain
				  float  Umax;			    // Parameter: upper saturation limit
				  float  Umin;			    // Parameter: lower saturation limit
				  float  up;				// Data: proportional term
				  float  ui;				// Data: integral term
				  float  v1;				// Data: pre-saturated controller output
				  float  i1;				// Data: integrator storage: ui(k-1)
				  float dumax;

} PI_CONTROLLER;

/*-----------------------------------------------------------------------------
Default initalisation values for the PI_GRANDO objects
-----------------------------------------------------------------------------*/                     

#define PI_CONTROLLER_DEFAULTS {		\
						   0, 			\
                           0, 			\
						   0, 			\
                           1.0,	        \
                           0.0,	        \
                           1.0,	        \
                           -1.0, 	    \
                           0.0,	        \
                           0.0, 	    \
                           0.0,	        \
                           0.0,	        \
                           1.0 	        \
              			  }


/*------------------------------------------------------------------------------
 	PI_GRANDO Macro Definition
------------------------------------------------------------------------------*/

#define PI_MACRO(v)												\
																\
	/* proportional term */ 									\
	v.up = v.Ref - v.Fbk;										\
																\
	/* integral term */ 										\
	v.ui = (v.Out == v.v1)?( v.Ki * v.up+ v.i1) : v.i1;	        \
	v.i1 = v.ui;												\
																\
	/* control output */ 										\
	v.v1 = v.Kp* (v.up + v.ui);							        \
	v.Out= _IQsat(v.v1, v.Umax, v.Umin);						\
	//v.w1 = (v.Out == v.v1) ? _IQ(1.0) : _IQ(0.0);				\


	
#endif // __PI_H__

