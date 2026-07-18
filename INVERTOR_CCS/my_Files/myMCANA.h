/*
 * myMCANA.h
 *
 *  Created on: 21.01.2026
 *      Author: Oleg Pavlovsky
 */

#ifndef MYMCANA_H_
#define MYMCANA_H_

//
// Defines
//
#define MCAN_STD_ID_FILTER_NUM (1U)
#define MCAN_EXT_ID_FILTER_NUM (0U)
#define MCAN_FIFO_0_NUM (64U)
#define MCAN_FIFO_0_ELEM_SIZE (MCAN_ELEM_SIZE_8BYTES)
#define MCAN_FIFO_1_NUM (0U)
#define MCAN_FIFO_1_ELEM_SIZE (MCAN_ELEM_SIZE_8BYTES)
#define MCAN_RX_BUFF_NUM (0U)
#define MCAN_RX_BUFF_ELEM_SIZE (MCAN_ELEM_SIZE_8BYTES)
#define MCAN_TX_BUFF_SIZE (32U)
#define MCAN_TX_FQ_SIZE (0U)
#define MCAN_TX_BUFF_ELEM_SIZE (MCAN_ELEM_SIZE_8BYTES)
#define MCAN_TX_EVENT_SIZE (0U)
#define MCAN_EXT_ID_AND_MASK (0x1FFFFFFFU)
#define MCAN_MSG_INT (0x81200)

#define MCANSS_TX_BUFFER_ELEM_ID_SHIFT                           (0U)
#define MCANSS_TX_BUFFER_ELEM_ID_MASK                            (0x1FFFFFFFU)
#define MCANSS_TX_BUFFER_ELEM_RTR_SHIFT                          (29U)
#define MCANSS_TX_BUFFER_ELEM_RTR_MASK                           (0x20000000U)
#define MCANSS_TX_BUFFER_ELEM_XTD_SHIFT                          (30U)
#define MCANSS_TX_BUFFER_ELEM_XTD_MASK                           (0x40000000U)
#define MCANSS_TX_BUFFER_ELEM_ESI_SHIFT                          (31U)
#define MCANSS_TX_BUFFER_ELEM_ESI_MASK                           (0x80000000U)
#define MCANSS_TX_BUFFER_ELEM_DLC_SHIFT                          (16U)
#define MCANSS_TX_BUFFER_ELEM_DLC_MASK                           (0x000F0000U)
#define MCANSS_TX_BUFFER_ELEM_BRS_SHIFT                          (20U)
#define MCANSS_TX_BUFFER_ELEM_BRS_MASK                           (0x00100000U)
#define MCANSS_TX_BUFFER_ELEM_FDF_SHIFT                          (21U)
#define MCANSS_TX_BUFFER_ELEM_FDF_MASK                           (0x00200000U)
#define MCANSS_TX_BUFFER_ELEM_EFC_SHIFT                          (23U)
#define MCANSS_TX_BUFFER_ELEM_EFC_MASK                           (0x00800000U)
#define MCANSS_TX_BUFFER_ELEM_MM_SHIFT                           (24U)
#define MCANSS_TX_BUFFER_ELEM_MM_MASK                            (0xFF000000U)

//
//  Defining Starting Addresses for Message RAM Sections,
//  (Calculated from Macros based on User defined configuration above)
//
#define MCAN_STD_ID_FILT_START_ADDR (0x0U)
#define MCAN_EXT_ID_FILT_START_ADDR                                            \
  (MCAN_STD_ID_FILT_START_ADDR +                                               \
   ((MCAN_STD_ID_FILTER_NUM * MCANSS_STD_ID_FILTER_SIZE_WORDS * 4U)))
#define MCAN_FIFO_0_START_ADDR                                                 \
  (MCAN_EXT_ID_FILT_START_ADDR +                                               \
   ((MCAN_EXT_ID_FILTER_NUM * MCANSS_EXT_ID_FILTER_SIZE_WORDS * 4U)))
#define MCAN_FIFO_1_START_ADDR                                                 \
  (MCAN_FIFO_0_START_ADDR +                                                    \
   (MCAN_getMsgObjSize(MCAN_FIFO_0_ELEM_SIZE) * 4U * MCAN_FIFO_0_NUM))
#define MCAN_RX_BUFF_START_ADDR                                                \
  (MCAN_FIFO_1_START_ADDR +                                                    \
   (MCAN_getMsgObjSize(MCAN_FIFO_1_ELEM_SIZE) * 4U * MCAN_FIFO_1_NUM))
#define MCAN_TX_BUFF_START_ADDR                                                \
  (MCAN_RX_BUFF_START_ADDR +                                                   \
   (MCAN_getMsgObjSize(MCAN_RX_BUFF_ELEM_SIZE) * 4U * MCAN_RX_BUFF_NUM))
#define MCAN_TX_EVENT_START_ADDR                                               \
  (MCAN_TX_BUFF_START_ADDR + (MCAN_getMsgObjSize(MCAN_TX_BUFF_ELEM_SIZE) *     \
                              4U * (MCAN_TX_BUFF_SIZE + MCAN_TX_FQ_SIZE)))

#endif /* MYMCANA_H_ */

