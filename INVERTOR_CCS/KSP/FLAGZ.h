#ifndef FLAGZ_H_
#define FLAGZ_H_

/*==============================================================================
	 	Структура  флагов (битовые поля) управления от PC
		макрос FLAGz_DEF_MACRO срабатывает при нажатии
		кнопки отключить и при выключении программы (закрытии формы)
=================================================================================*/
#include "Motor.h"
#include "f28p55x_device.h"

struct SharpFlag{
    Uint32  ramp_Off:1;         //0 (MBOX28 cmd = 6) вкл/выкл режима ограничениея скорости изменения частоты магнитного поля
    Uint32  rez1:1;             //1 (MBOX28 cmd = 50) вкл/выкл режима ограничениея частоты магнитного поля по скольжению.
    Uint32  transistor_imit:1;  //2

    Uint32  ResolverInverse:1;  //3 инвертирует сигнал Резолвера
    Uint32  Reverse:1;          //4 инвертирует направление вращения вала спарки
    Uint32  rez5:1;             //5
    Uint32  rez6:1;             //6

    Uint32 	rez7:1;             //7 требование выполнить калибровку  = 1 иниц.
	Uint32  rez8:1;             //8 заданное скольжение от поля или оборотов
	Uint32  rez9:1;             //9


	Uint32  rez10:1;            //10
	Uint32  rez11:1;            //11
	Uint32  rez12:1;            //12 меняет положение генератор/двигатель если = 0 то двигатель слева т.е. АИН1
	Uint32  rez13:1;	        //13 флаг держать заданные обороты двигателя
	Uint32  stability:1;        //14
	Uint32  ain2_Disable:1;     //15

	Uint32  heat_resistor:1;    //16
	Uint32  rez17:1;            //17
	Uint32  rez18:1;            //18 =1 выводить средние значения токов Iav, =0 Irms
	Uint32  rez19:1;            //19
	Uint32  V_regul:1;          //20

	Uint32  KVU_On:1;           //21 =1 Задание SpeedRef от КВУ
	Uint32  ain1_Disable:1;     //22  =1 запрещает включение ШИМ АИН1
	Uint32  restore:1;          //23
	Uint32  need_break:1;       //24
	Uint32  inst_volts:1;       //25
	Uint32	gain_nullqep1:1;    //26  кнопка "Limit"-активна разрешает изменять напряжение на двигателе когда нет вращения магнитного поля.


	Uint32  CAN_header_init:1;  //27

	Uint32  RPM_imit:1;         //28
	Uint32  pusk:1;             //29 кнопка "Пуск" 1- активна,
	Uint32  stop:1;             //30 кнопка "Стоп" 1- активна,
	Uint32  connect:1;          //31 кнопка "Подключить" 1- была нажата,

	};
//-------------------------------------------------------------------------------------

  union PCflag{
        struct SharpFlag bit;
  		Uint32 all;
	};


  //-------------------------------------------------------------------------------------
  struct StendFlags{

      uint16_t  offset_flag:1;      //0

      };

    union StendFlag{
          struct StendFlags bit;
          uint16_t all;
      };

    //-------------------------------------------------------------------------------------
     struct StendErrors{

         uint16_t  offset_flag:1;      //0

         };

       union StendError{
             struct StendErrors bit;
             uint16_t all;
         };

//------------------------------------------------------------------------------
  struct CCS_Flag {
     Uint32   tooth_Syncro: 1;      //0
     Uint32   Udc_IGBToff:  1;      //1
     Uint32   rezerv2:      1;      //2
     Uint32   rezerv3:      1;      //3

     Uint32   I1a_Overload: 1;      //4
     Uint32   I1b_Overload: 1;      //5
     Uint32   I1c_Overload: 1;      //6

     Uint32   I2a_Overload: 1;      //7
     Uint32   I2b_Overload: 1;      //8
     Uint32   I2c_Overload: 1;      //9



     Uint32   OPTIC1_Err1:  1; //10 T1
     Uint32   OPTIC1_Err2:  1; //11 T2
     Uint32   OPTIC1_Err3:  1; //12 T3
     Uint32   OPTIC1_Err4:  1; //13 T4
     Uint32   OPTIC1_Err5:  1; //14 T5
     Uint32   OPTIC1_Err6:  1; //15 T6
     Uint32   OPTIC1_Err7:  1; //16 RT1
     Uint32   AIN1_trig:    1; //17 Триггер АИН1 Мотор  GpioDataRegs.GPADAT.bit.GPIO16
     Uint32   RT1_trig:     1; //18 Триггер RT1

     Uint32   OPTIC2_Err1:  1; //19 T1
     Uint32   OPTIC2_Err2:  1; //20 T2
     Uint32   OPTIC2_Err3:  1; //21 T3
     Uint32   OPTIC2_Err4:  1; //22 T4
     Uint32   OPTIC2_Err5:  1; //23 T5
     Uint32   OPTIC2_Err6:  1; //24 T6
     Uint32   OPTIC2_Err7:  1; //25 RT2
     Uint32   AIN2_trig:    1; //26 Триггер АИН2 Мотор  GpioDataRegs.GPADAT.bit.GPIO17;
     Uint32   RT2_trig:     1; //27 выход RS тригера захвата ошибки по оптике тормозного резистора
     Uint32   RESET_trig:   1; //28 сброс тригеров оптических неисправностей  GpioDataRegs.GPCDAT.bit.GPIO71;


     Uint32   RT1_On:       1;//29 GpioDataRegs.GPADAT.bit.GPIO26;    // выход управления тормозного резистора  RT1
     Uint32   RT2_On:       1;//30 GpioDataRegs.GPADAT.bit.GPIO27;     // выход управления тормозного резистора RT2

     Uint32   Resolver_Fault:         1;      //31


  };

  struct CCS_Uint16 {
      uint16_t    u1;
      uint16_t    u2;
  };
  //-------------------------------------------------------------------------------------

    union CCS{
          struct CCS_Flag bit;
          struct CCS_Uint16 u16;
          Uint32 all;
      };

    extern union CCS ccs;
   //---------------------------------------------------------------------------------------
   // флаги срабатывания защиты в момент отключения IGBT.
    // struct Overload / union ErrorFlags / overload убраны: поля Ia..Ic и Idc
    // описывали трёхфазник B81, и ни одного чтения этой переменной в проекте
    // не осталось. Перегрузки по току живут в таблице ошибок (Errors.c).
   //-------------------------------------------------------------------------------------------

    typedef struct  {
         Uint32   rg2: 1;      //1
         Uint32   em2:  1;     //2
         Uint32   r3:      1;  //3
         Uint32   r4:      1;  //4

         Uint32   r5: 1;      //5
         Uint32   r6: 1;      //6
         Uint32   r7: 1;      //7

         Uint32   r8: 1;      //8
         Uint32   r9: 1;      //9
         Uint32   r10: 1;      //10



         Uint32   r11:  1; //11 T1
         Uint32   r12:  1; //12 T2
         Uint32   r13:  1; //13 T3
         Uint32   r14:  1; //14 T4
         Uint32   r15:  1; //15 T5
         Uint32   r16:  1; //16 T6
         Uint32   r17:  1; //17 RT1
         Uint32   r18:  1; //18 Триггер АИН1 Мотор  GpioDataRegs.GPADAT.bit.GPIO16
         Uint32   r19:  1; //19 Триггер RT1

         Uint32   r20:  1; //20 T1
         Uint32   r21:  1; //21 T2
         Uint32   r22:  1; //22 T3
         Uint32   r23:  1; //23 T4
         Uint32   r24:  1; //24 T5
         Uint32   r25:  1; //25 T6
         Uint32   r26:  1; //26 RT2
         Uint32   r27:    1; //27 Триггер АИН2 Мотор  GpioDataRegs.GPADAT.bit.GPIO17;
         Uint32   r28:     1; //28 выход RS тригера захвата ошибки по оптике тормозного резистора
         Uint32   r29:   1; //29 сброс тригеров оптических неисправностей  GpioDataRegs.GPCDAT.bit.GPIO71;


         Uint32   r30:       1;//30 GpioDataRegs.GPADAT.bit.GPIO26;    // выход управления тормозного резистора  RT1
         Uint32   r31:       1;//31 GpioDataRegs.GPADAT.bit.GPIO27;     // выход управления тормозного резистора RT2

         Uint32   r32:         1;//32


      } Sharp2;


    union SHARP2{
          Sharp2 bit;
          Uint32 all;
      };



typedef struct {	uint16_t  send_id:1;
					uint16_t  stop_send:1;
					uint16_t  stop_mail:1;

		   } CAN_FLAGz;

#define CAN_flag_DEFAULTS   {   0,    			/*  send_id  	*/     	  \
								0,    			/*  stop_send  */      	  \
								0,    			/*  stop_mail  */     	  \
							}


#endif /* FLAGZ_H_ */

