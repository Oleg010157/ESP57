/*
 * MotorCalc.h
 *
 *  Created on: 14 Nov 2019
 *      Author: User
 */

#ifndef MOTORCALC_H_
#define MOTORCALC_H_

//#include <system_constants.h>
#include "config_switch.h"
#include "Motor.h"
#include "PARAMS_INOUT.h"


#include "VOLT_CALC_CLA.h"




void RMS(motor *m);
void Um_Lim(motor *m, float Urms);
void fizika(motor *m);
void tok_immit(motor *m);
void T_shema (motor *m);

void clark(motor *v);

void KeyOnInit(motor *v);
void PUSK_Init(motor *v);
void park(motor *v);



void pi_Init(PI_CONTROLLER *v);
void piId_Init(motor *m);
void piIq_Init(motor *m);
void piSRPM_Init(motor *m);
void piRPM_Init(motor *m);
void pi_reg(PI_CONTROLLER *v);
void piId_reg(PI_CONTROLLER *v);



//inline float satur(float in);
float saturation(float input, float high, float low);
void slip(motor *m);

void Im_reg(motor *m);
void Id_reg(motor *m);
void Iq_reg(motor *m);
void Irpm_reg(motor *m);
void freq_Ref(motor *m);

void LR_TEST(motor *m);


void CUR_MOD(motor *m);
void CUR_CONST(motor *m);
void Obsorver1(motor *m);
void ACI_FE(motor *m);
void ACI_SE(motor *m);

void Reset_stend_filtr(void);

void GenKSP_VToff(void);



#endif /* MOTORCALC_H_ */
