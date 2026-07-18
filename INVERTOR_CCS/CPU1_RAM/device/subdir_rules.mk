################################################################################
# Automatically-generated file. Do not edit!
################################################################################

SHELL = cmd.exe

# Each subdirectory must supply rules for building sources it contributes
device/%.obj: ../device/%.c $(GEN_OPTS) | $(GEN_FILES) $(GEN_MISC_FILES)
	@echo 'C2000 Compiler - building file: "$<"'
	"C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS/bin/cl2000" -v28 -ml -mt --cla_support=cla2 --float_support=fpu32 --idiv_support=idiv0 --tmu_support=tmu1 --vcu_support=vcu2 -Ooff --fp_mode=relaxed --include_path="C:/Users/M4800/Desktop/P55_Work/K1/INVERTOR_CCS" --include_path="C:/ti/C2000Ware_26_01_00_00/device_support/f28p55x/headers/include/" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/INVERTOR_CCS/device" --include_path="C:/ti/C2000Ware_26_01_00_00/driverlib/f28p55x/driverlib/" --include_path="C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS/include" --include_path="C:/ti/C2000Ware_26_01_00_00/device_support/f28p55x/common/include" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/bin" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/INVERTOR_CCS/KSP" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/INVERTOR_CCS/CLA" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/INVERTOR_CCS/my_Files" --include_path="C:/ti/C2000Ware_26_01_00_00/driverlib/f28p55x/driverlib/inc" --include_path="C:/ti/C2000Ware_26_01_00_00/libraries/math/CLAmath/c28/include" --include_path="C:/ti/C2000Ware_26_01_00_00/libraries/flash_api/f28p55x/include/FlashAPI" --include_path="C:/ti/C2000Ware_26_01_00_00/libraries/flash_api/f28p55x/include/FlashAPI/Constants" --include_path="C200WARE_COMMON_INCLUDE" --define=CPU1 --define=_DUAL_HEADERS --diag_suppress=10063 --diag_warning=225 --diag_wrap=off --display_error_number --abi=eabi --cla_signed_compare_workaround=on --preproc_with_compile --preproc_dependency="device/$(basename $(<F)).d_raw" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/INVERTOR_CCS/CPU1_RAM/syscfg" --obj_directory="device" $(GEN_OPTS__FLAG) "$<"
	@echo 'Finished building: "$<"'
	@echo ' '


