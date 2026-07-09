################################################################################
# Automatically-generated file. Do not edit!
################################################################################

SHELL = cmd.exe

# Add inputs and outputs from these tool invocations to the build variables 
CMD_SRCS += \
../28p55x_generic_ram_lnk.cmd \
../f28p55x_headers_nonBIOS.cmd 

SYSCFG_SRCS += \
../c2000.syscfg 

LIB_SRCS += \
C:/ti/C2000Ware_26_01_00_00/driverlib/f28p55x/driverlib/ccs/Debug/driverlib.lib 

ASM_SRCS += \
C:/ti/C2000Ware_26_01_00_00/device_support/f28p55x/common/source/f28p55x_codestartbranch.asm \
../f28p55x_codestartbranch.asm \
../f28p55x_usdelay.asm 

C_SRCS += \
./syscfg/board.c \
./syscfg/device.c \
./syscfg/c2000ware_libraries.c \
../f28p55x_defaultisr.c \
../f28p55x_globalvariabledefs.c \
../f28p55x_gpio.c \
../f28p55x_piectrl.c \
../f28p55x_pievect.c \
../f28p55x_sysctrl.c \
../main.c \
../myMCAN.c 

GEN_FILES += \
./syscfg/board.c \
./syscfg/board.opt \
./syscfg/device.c \
./syscfg/c2000ware_libraries.opt \
./syscfg/c2000ware_libraries.c 

GEN_MISC_DIRS += \
./syscfg 

C_DEPS += \
./syscfg/board.d \
./syscfg/device.d \
./syscfg/c2000ware_libraries.d \
./f28p55x_defaultisr.d \
./f28p55x_globalvariabledefs.d \
./f28p55x_gpio.d \
./f28p55x_piectrl.d \
./f28p55x_pievect.d \
./f28p55x_sysctrl.d \
./main.d \
./myMCAN.d 

GEN_OPTS += \
./syscfg/board.opt \
./syscfg/c2000ware_libraries.opt 

OBJS += \
./syscfg/board.obj \
./syscfg/device.obj \
./syscfg/c2000ware_libraries.obj \
./f28p55x_codestartbranch.obj \
./f28p55x_defaultisr.obj \
./f28p55x_globalvariabledefs.obj \
./f28p55x_gpio.obj \
./f28p55x_piectrl.obj \
./f28p55x_pievect.obj \
./f28p55x_sysctrl.obj \
./f28p55x_usdelay.obj \
./main.obj \
./myMCAN.obj 

ASM_DEPS += \
./f28p55x_codestartbranch.d \
./f28p55x_codestartbranch.d \
./f28p55x_usdelay.d 

GEN_MISC_FILES += \
./syscfg/board.h \
./syscfg/board.cmd.genlibs \
./syscfg/board.json \
./syscfg/pinmux.csv \
./syscfg/device.h \
./syscfg/c2000ware_libraries.cmd.genlibs \
./syscfg/c2000ware_libraries.h \
./syscfg/clocktree.h 

GEN_MISC_DIRS__QUOTED += \
"syscfg" 

OBJS__QUOTED += \
"syscfg\board.obj" \
"syscfg\device.obj" \
"syscfg\c2000ware_libraries.obj" \
"f28p55x_codestartbranch.obj" \
"f28p55x_defaultisr.obj" \
"f28p55x_globalvariabledefs.obj" \
"f28p55x_gpio.obj" \
"f28p55x_piectrl.obj" \
"f28p55x_pievect.obj" \
"f28p55x_sysctrl.obj" \
"f28p55x_usdelay.obj" \
"main.obj" \
"myMCAN.obj" 

GEN_MISC_FILES__QUOTED += \
"syscfg\board.h" \
"syscfg\board.cmd.genlibs" \
"syscfg\board.json" \
"syscfg\pinmux.csv" \
"syscfg\device.h" \
"syscfg\c2000ware_libraries.cmd.genlibs" \
"syscfg\c2000ware_libraries.h" \
"syscfg\clocktree.h" 

C_DEPS__QUOTED += \
"syscfg\board.d" \
"syscfg\device.d" \
"syscfg\c2000ware_libraries.d" \
"f28p55x_defaultisr.d" \
"f28p55x_globalvariabledefs.d" \
"f28p55x_gpio.d" \
"f28p55x_piectrl.d" \
"f28p55x_pievect.d" \
"f28p55x_sysctrl.d" \
"main.d" \
"myMCAN.d" 

GEN_FILES__QUOTED += \
"syscfg\board.c" \
"syscfg\board.opt" \
"syscfg\device.c" \
"syscfg\c2000ware_libraries.opt" \
"syscfg\c2000ware_libraries.c" 

ASM_DEPS__QUOTED += \
"f28p55x_codestartbranch.d" \
"f28p55x_codestartbranch.d" \
"f28p55x_usdelay.d" 

SYSCFG_SRCS__QUOTED += \
"../c2000.syscfg" 

C_SRCS__QUOTED += \
"./syscfg/board.c" \
"./syscfg/device.c" \
"./syscfg/c2000ware_libraries.c" \
"../f28p55x_defaultisr.c" \
"../f28p55x_globalvariabledefs.c" \
"../f28p55x_gpio.c" \
"../f28p55x_piectrl.c" \
"../f28p55x_pievect.c" \
"../f28p55x_sysctrl.c" \
"../main.c" \
"../myMCAN.c" 

ASM_SRCS__QUOTED += \
"C:/ti/C2000Ware_26_01_00_00/device_support/f28p55x/common/source/f28p55x_codestartbranch.asm" \
"../f28p55x_codestartbranch.asm" \
"../f28p55x_usdelay.asm" 


