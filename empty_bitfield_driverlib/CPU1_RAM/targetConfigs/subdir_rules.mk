################################################################################
# Automatically-generated file. Do not edit!
################################################################################

SHELL = cmd.exe

# Each subdirectory must supply rules for building sources it contributes
build-1721074938: ../targetConfigs/c2000.syscfg
	@echo 'SysConfig - building file: "$<"'
	"C:/ti/ccs2100/ccs/utils/sysconfig_1.28.0/sysconfig_cli.bat" --script "C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib/targetConfigs/c2000.syscfg" -o "syscfg" --compiler ccs
	@echo 'Finished building: "$<"'
	@echo ' '

syscfg/PinmuxConfigSummary.csv: build-1721074938 ../targetConfigs/c2000.syscfg
syscfg: build-1721074938


