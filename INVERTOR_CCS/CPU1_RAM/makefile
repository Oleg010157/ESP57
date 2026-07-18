################################################################################
# Automatically-generated file. Do not edit!
################################################################################

SHELL = cmd.exe

CG_TOOL_ROOT := C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS

GEN_OPTS__FLAG := --cmd_file="syscfg/board.opt" --cmd_file="syscfg/c2000ware_libraries.opt" 
GEN_CMDS__FLAG := 

ORDERED_OBJS += \
"./syscfg/board.obj" \
"./syscfg/device.obj" \
"./syscfg/c2000ware_libraries.obj" \
"./f28p55x_codestartbranch.obj" \
"./CLA/Task1.obj" \
"./CLA/Task7.obj" \
"./CLA/cla_shared.obj" \
"./KSP/CANA_UI.obj" \
"./KSP/Errors.obj" \
"./KSP/Functions.obj" \
"./KSP/GET_freq.obj" \
"./KSP/Kadr60.obj" \
"./KSP/MBOX28.obj" \
"./KSP/Main_ISR.obj" \
"./KSP/MotorCalc.obj" \
"./KSP/Power.obj" \
"./KSP/REF_Kadr.obj" \
"./KSP/main_Init.obj" \
"./KSP/pwm_funcs.obj" \
"./bitfield_source/f28p55x_defaultisr.obj" \
"./bitfield_source/f28p55x_globalvariabledefs.obj" \
"./bitfield_source/f28p55x_gpio.obj" \
"./bitfield_source/f28p55x_piectrl.obj" \
"./bitfield_source/f28p55x_pievect.obj" \
"./bitfield_source/f28p55x_sysctrl.obj" \
"./my_Files/SPI_EEPROM.obj" \
"./my_Files/main.obj" \
"./my_Files/myADC.obj" \
"./my_Files/myEPWM.obj" \
"./my_Files/myMCAN.obj" \
"../28p55x_generic_ram_lnk.cmd" \
"C:/ti/C2000Ware_26_01_00_00/driverlib/f28p55x/driverlib/ccs/Debug/driverlib.lib" \
"../f28p55x_headers_nonBIOS.cmd" \
"../my_Files/Bootloader/FAPI_F28003x_EABI.lib" \
$(GEN_CMDS__FLAG) \
-llibc.a \

-include ../makefile.init

RM := DEL /F
RMDIR := RMDIR /S/Q

# All of the sources participating in the build are defined here
-include sources.mk
-include subdir_vars.mk
-include CLA/subdir_vars.mk
-include KSP/subdir_vars.mk
-include bitfield_source/subdir_vars.mk
-include my_Files/Bootloader/subdir_vars.mk
-include my_Files/subdir_vars.mk
-include subdir_rules.mk
-include CLA/subdir_rules.mk
-include KSP/subdir_rules.mk
-include bitfield_source/subdir_rules.mk
-include my_Files/Bootloader/subdir_rules.mk
-include my_Files/subdir_rules.mk
-include objects.mk

ifneq ($(MAKECMDGOALS),clean)
ifneq ($(strip $(C55_DEPS)),)
-include $(C55_DEPS)
endif
ifneq ($(strip $(C_UPPER_DEPS)),)
-include $(C_UPPER_DEPS)
endif
ifneq ($(strip $(S67_DEPS)),)
-include $(S67_DEPS)
endif
ifneq ($(strip $(S62_DEPS)),)
-include $(S62_DEPS)
endif
ifneq ($(strip $(S_DEPS)),)
-include $(S_DEPS)
endif
ifneq ($(strip $(OPT_DEPS)),)
-include $(OPT_DEPS)
endif
ifneq ($(strip $(C??_DEPS)),)
-include $(C??_DEPS)
endif
ifneq ($(strip $(ASM_UPPER_DEPS)),)
-include $(ASM_UPPER_DEPS)
endif
ifneq ($(strip $(S??_DEPS)),)
-include $(S??_DEPS)
endif
ifneq ($(strip $(C64_DEPS)),)
-include $(C64_DEPS)
endif
ifneq ($(strip $(CXX_DEPS)),)
-include $(CXX_DEPS)
endif
ifneq ($(strip $(S64_DEPS)),)
-include $(S64_DEPS)
endif
ifneq ($(strip $(CLA_DEPS)),)
-include $(CLA_DEPS)
endif
ifneq ($(strip $(S55_DEPS)),)
-include $(S55_DEPS)
endif
ifneq ($(strip $(SV7A_DEPS)),)
-include $(SV7A_DEPS)
endif
ifneq ($(strip $(C62_DEPS)),)
-include $(C62_DEPS)
endif
ifneq ($(strip $(C67_DEPS)),)
-include $(C67_DEPS)
endif
ifneq ($(strip $(K_DEPS)),)
-include $(K_DEPS)
endif
ifneq ($(strip $(C_DEPS)),)
-include $(C_DEPS)
endif
ifneq ($(strip $(CC_DEPS)),)
-include $(CC_DEPS)
endif
ifneq ($(strip $(C++_DEPS)),)
-include $(C++_DEPS)
endif
ifneq ($(strip $(C43_DEPS)),)
-include $(C43_DEPS)
endif
ifneq ($(strip $(S43_DEPS)),)
-include $(S43_DEPS)
endif
ifneq ($(strip $(ASM_DEPS)),)
-include $(ASM_DEPS)
endif
ifneq ($(strip $(S_UPPER_DEPS)),)
-include $(S_UPPER_DEPS)
endif
ifneq ($(strip $(CPP_DEPS)),)
-include $(CPP_DEPS)
endif
ifneq ($(strip $(SA_DEPS)),)
-include $(SA_DEPS)
endif
endif

-include ../makefile.defs

# Add inputs and outputs from these tool invocations to the build variables 
EXE_OUTPUTS += \
invertor.out 

EXE_OUTPUTS__QUOTED += \
"invertor.out" 


# All Target
all: $(OBJS) $(CMD_SRCS) $(LIB_SRCS) $(GEN_CMDS)
	@$(MAKE) --no-print-directory -Onone "invertor.out"

# Tool invocations
invertor.out: $(OBJS) $(CMD_SRCS) $(LIB_SRCS) $(GEN_CMDS)
	@echo 'C2000 Linker - building target: "$@"'
	"C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS/bin/cl2000" -v28 -ml -mt --cla_support=cla2 --float_support=fpu32 --idiv_support=idiv0 --tmu_support=tmu1 --vcu_support=vcu2 -Ooff --fp_mode=relaxed --define=CPU1 --define=_DUAL_HEADERS --diag_suppress=10063 --diag_warning=225 --diag_wrap=off --display_error_number --abi=eabi --cla_signed_compare_workaround=on -z -m"invertor.map" --stack_size=0x100 --warn_sections -i"C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS/lib" -i"C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS/include" --reread_libs --diag_wrap=off --display_error_number --xml_link_info="invertor_linkInfo.xml" --entry_point=code_start --rom_model -o "invertor.out" $(ORDERED_OBJS)
	@echo 'Finished building target: "$@"'
	@echo ' '

# Other Targets
clean:
	-$(RM) $(GEN_MISC_FILES__QUOTED)$(GEN_FILES__QUOTED)$(EXE_OUTPUTS__QUOTED)
	-$(RM) "syscfg\board.obj" "syscfg\device.obj" "syscfg\c2000ware_libraries.obj" "f28p55x_codestartbranch.obj" "CLA\Task1.obj" "CLA\Task7.obj" "CLA\cla_shared.obj" "KSP\CANA_UI.obj" "KSP\Errors.obj" "KSP\Functions.obj" "KSP\GET_freq.obj" "KSP\Kadr60.obj" "KSP\MBOX28.obj" "KSP\Main_ISR.obj" "KSP\MotorCalc.obj" "KSP\Power.obj" "KSP\REF_Kadr.obj" "KSP\main_Init.obj" "KSP\pwm_funcs.obj" "bitfield_source\f28p55x_defaultisr.obj" "bitfield_source\f28p55x_globalvariabledefs.obj" "bitfield_source\f28p55x_gpio.obj" "bitfield_source\f28p55x_piectrl.obj" "bitfield_source\f28p55x_pievect.obj" "bitfield_source\f28p55x_sysctrl.obj" "my_Files\SPI_EEPROM.obj" "my_Files\main.obj" "my_Files\myADC.obj" "my_Files\myEPWM.obj" "my_Files\myMCAN.obj" 
	-$(RM) "syscfg\board.d" "syscfg\device.d" "syscfg\c2000ware_libraries.d" "CLA\cla_shared.d" "KSP\CANA_UI.d" "KSP\Errors.d" "KSP\Functions.d" "KSP\GET_freq.d" "KSP\Kadr60.d" "KSP\MBOX28.d" "KSP\Main_ISR.d" "KSP\MotorCalc.d" "KSP\Power.d" "KSP\REF_Kadr.d" "KSP\main_Init.d" "KSP\pwm_funcs.d" "bitfield_source\f28p55x_defaultisr.d" "bitfield_source\f28p55x_globalvariabledefs.d" "bitfield_source\f28p55x_gpio.d" "bitfield_source\f28p55x_piectrl.d" "bitfield_source\f28p55x_pievect.d" "bitfield_source\f28p55x_sysctrl.d" "my_Files\SPI_EEPROM.d" "my_Files\main.d" "my_Files\myADC.d" "my_Files\myEPWM.d" "my_Files\myMCAN.d" 
	-$(RM) "CLA\Task1.d" "CLA\Task7.d" 
	-$(RM) "f28p55x_codestartbranch.d" 
	-$(RMDIR) $(GEN_MISC_DIRS__QUOTED)
	-@echo ' '

.PHONY: all clean dependents
.SECONDARY:

-include ../makefile.targets

