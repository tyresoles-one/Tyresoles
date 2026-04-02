/* eslint-disable */
import type { TypedDocumentNode as DocumentNode } from '@graphql-typed-document-node/core';
export type Maybe<T> = T | null;
export type InputMaybe<T> = Maybe<T>;
export type Exact<T extends { [key: string]: unknown }> = { [K in keyof T]: T[K] };
export type MakeOptional<T, K extends keyof T> = Omit<T, K> & { [SubKey in K]?: Maybe<T[SubKey]> };
export type MakeMaybe<T, K extends keyof T> = Omit<T, K> & { [SubKey in K]: Maybe<T[SubKey]> };
export type MakeEmpty<T extends { [key: string]: unknown }, K extends keyof T> = { [_ in K]?: never };
export type Incremental<T> = T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
/** All built-in and custom scalars, mapped to their actual values */
export type Scalars = {
  ID: { input: string; output: string; }
  String: { input: string; output: string; }
  Boolean: { input: boolean; output: boolean; }
  Int: { input: number; output: number; }
  Float: { input: number; output: number; }
  /** The `Byte` scalar type represents non-fractional whole numeric values. Byte can represent values between 0 and 255. */
  Byte: { input: any; output: any; }
  /** The `Date` scalar represents an ISO-8601 compliant date type. */
  Date: { input: any; output: any; }
  /** The `DateTime` scalar represents an ISO-8601 compliant date time type. */
  DateTime: { input: any; output: any; }
  /** The built-in `Decimal` scalar type. */
  Decimal: { input: any; output: any; }
  /** The `Long` scalar type represents non-fractional signed whole 64-bit numeric values. Long can represent values between -(2^63) and 2^63 - 1. */
  Long: { input: any; output: any; }
  UUID: { input: any; output: any; }
};

export type AccountTransaction = {
  __typename?: 'AccountTransaction';
  amount: Scalars['Decimal']['output'];
  balance: Scalars['Decimal']['output'];
  customerName: Scalars['String']['output'];
  customerNo: Scalars['String']['output'];
  date?: Maybe<Scalars['DateTime']['output']>;
  documentNo: Scalars['String']['output'];
  type: Scalars['Int']['output'];
};

export type AccountTransactionFilterInput = {
  amount?: InputMaybe<DecimalOperationFilterInput>;
  and?: InputMaybe<Array<AccountTransactionFilterInput>>;
  balance?: InputMaybe<DecimalOperationFilterInput>;
  customerName?: InputMaybe<StringOperationFilterInput>;
  customerNo?: InputMaybe<StringOperationFilterInput>;
  date?: InputMaybe<DateTimeOperationFilterInput>;
  documentNo?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<AccountTransactionFilterInput>>;
  type?: InputMaybe<IntOperationFilterInput>;
};

export type AccountTransactionSortInput = {
  amount?: InputMaybe<SortEnumType>;
  balance?: InputMaybe<SortEnumType>;
  customerName?: InputMaybe<SortEnumType>;
  customerNo?: InputMaybe<SortEnumType>;
  date?: InputMaybe<SortEnumType>;
  documentNo?: InputMaybe<SortEnumType>;
  type?: InputMaybe<SortEnumType>;
};

export type Area = {
  __typename?: 'Area';
  code?: Maybe<Scalars['String']['output']>;
  deliveryTerms?: Maybe<Scalars['String']['output']>;
  distanceFromFactory: Scalars['Decimal']['output'];
  hide: Scalars['Byte']['output'];
  name?: Maybe<Scalars['String']['output']>;
  responsibilityCenter?: Maybe<Scalars['String']['output']>;
  team?: Maybe<Scalars['String']['output']>;
  vehicles?: Maybe<Scalars['String']['output']>;
};

export type AreaFilterInput = {
  and?: InputMaybe<Array<AreaFilterInput>>;
  code?: InputMaybe<StringOperationFilterInput>;
  deliveryTerms?: InputMaybe<StringOperationFilterInput>;
  distanceFromFactory?: InputMaybe<DecimalOperationFilterInput>;
  hide?: InputMaybe<ByteOperationFilterInput>;
  name?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<AreaFilterInput>>;
  responsibilityCenter?: InputMaybe<StringOperationFilterInput>;
  team?: InputMaybe<StringOperationFilterInput>;
  vehicles?: InputMaybe<StringOperationFilterInput>;
};

export type AreaSortInput = {
  code?: InputMaybe<SortEnumType>;
  deliveryTerms?: InputMaybe<SortEnumType>;
  distanceFromFactory?: InputMaybe<SortEnumType>;
  hide?: InputMaybe<SortEnumType>;
  name?: InputMaybe<SortEnumType>;
  responsibilityCenter?: InputMaybe<SortEnumType>;
  team?: InputMaybe<SortEnumType>;
  vehicles?: InputMaybe<SortEnumType>;
};

export type AttendeeInput = {
  isRequired: Scalars['Boolean']['input'];
  userId: Scalars['String']['input'];
};

export type BuyerDetail = {
  __typename?: 'BuyerDetail';
  addr1: Scalars['String']['output'];
  addr2?: Maybe<Scalars['String']['output']>;
  em?: Maybe<Scalars['String']['output']>;
  gstin: Scalars['String']['output'];
  lglNm: Scalars['String']['output'];
  loc: Scalars['String']['output'];
  ph?: Maybe<Scalars['String']['output']>;
  pin: Scalars['Int']['output'];
  pos: Scalars['String']['output'];
  stcd: Scalars['String']['output'];
  trdNm?: Maybe<Scalars['String']['output']>;
};

export type ByteOperationFilterInput = {
  eq?: InputMaybe<Scalars['Byte']['input']>;
  gt?: InputMaybe<Scalars['Byte']['input']>;
  gte?: InputMaybe<Scalars['Byte']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['Byte']['input']>>>;
  lt?: InputMaybe<Scalars['Byte']['input']>;
  lte?: InputMaybe<Scalars['Byte']['input']>;
  neq?: InputMaybe<Scalars['Byte']['input']>;
  ngt?: InputMaybe<Scalars['Byte']['input']>;
  ngte?: InputMaybe<Scalars['Byte']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['Byte']['input']>>>;
  nlt?: InputMaybe<Scalars['Byte']['input']>;
  nlte?: InputMaybe<Scalars['Byte']['input']>;
};

export type CalendarAuditLogEntryDto = {
  __typename?: 'CalendarAuditLogEntryDto';
  action: Scalars['Int']['output'];
  createdAtUtc: Scalars['DateTime']['output'];
  eventId: Scalars['UUID']['output'];
  id: Scalars['Long']['output'];
  payload?: Maybe<Scalars['String']['output']>;
  userId: Scalars['String']['output'];
};

export type CalendarEventDto = {
  __typename?: 'CalendarEventDto';
  attendees: Array<EventAttendeeDto>;
  description?: Maybe<Scalars['String']['output']>;
  endUtc: Scalars['DateTime']['output'];
  eventTypeColor?: Maybe<Scalars['String']['output']>;
  eventTypeId?: Maybe<Scalars['Int']['output']>;
  eventTypeName?: Maybe<Scalars['String']['output']>;
  exceptionOccurrenceStartUtc?: Maybe<Scalars['DateTime']['output']>;
  id: Scalars['UUID']['output'];
  isAllDay: Scalars['Boolean']['output'];
  location?: Maybe<Scalars['String']['output']>;
  meetingLink?: Maybe<Scalars['String']['output']>;
  ownerUserId: Scalars['String']['output'];
  parentEventId?: Maybe<Scalars['UUID']['output']>;
  recurrence?: Maybe<RecurrenceDto>;
  recurrenceRuleId?: Maybe<Scalars['UUID']['output']>;
  reminders: Array<ReminderDto>;
  showAs: Scalars['Int']['output'];
  startUtc: Scalars['DateTime']['output'];
  status: Scalars['Int']['output'];
  tags: Array<EventTagDto>;
  tasks: Array<CalendarTaskDto>;
  timeZoneId?: Maybe<Scalars['String']['output']>;
  title: Scalars['String']['output'];
  visibility: Scalars['Int']['output'];
};

export type CalendarShareDto = {
  __typename?: 'CalendarShareDto';
  createdAt: Scalars['DateTime']['output'];
  id: Scalars['UUID']['output'];
  ownerUserId: Scalars['String']['output'];
  permission: Scalars['Int']['output'];
  sharedWithUserId: Scalars['String']['output'];
};

export type CalendarTaskDto = {
  __typename?: 'CalendarTaskDto';
  id: Scalars['UUID']['output'];
  isCompleted: Scalars['Boolean']['output'];
  parentTaskId?: Maybe<Scalars['UUID']['output']>;
  sortOrder: Scalars['Int']['output'];
  subTasks: Array<CalendarTaskDto>;
  title: Scalars['String']['output'];
};

export type CalendarTaskInput = {
  id?: InputMaybe<Scalars['UUID']['input']>;
  isCompleted: Scalars['Boolean']['input'];
  parentTaskId?: InputMaybe<Scalars['UUID']['input']>;
  sortOrder: Scalars['Int']['input'];
  subTasks?: InputMaybe<Array<CalendarTaskInput>>;
  title: Scalars['String']['input'];
};

export type CasingItem = {
  __typename?: 'CasingItem';
  category: Scalars['String']['output'];
  code: Scalars['String']['output'];
  maxRate: Scalars['String']['output'];
  minRate: Scalars['String']['output'];
  name: Scalars['String']['output'];
};

export type CasingItemFilterInput = {
  and?: InputMaybe<Array<CasingItemFilterInput>>;
  category?: InputMaybe<StringOperationFilterInput>;
  code?: InputMaybe<StringOperationFilterInput>;
  maxRate?: InputMaybe<StringOperationFilterInput>;
  minRate?: InputMaybe<StringOperationFilterInput>;
  name?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<CasingItemFilterInput>>;
};

export type CasingItemInput = {
  category: Scalars['String']['input'];
  code: Scalars['String']['input'];
  maxRate: Scalars['String']['input'];
  minRate: Scalars['String']['input'];
  name: Scalars['String']['input'];
};

export type CasingItemSortInput = {
  category?: InputMaybe<SortEnumType>;
  code?: InputMaybe<SortEnumType>;
  maxRate?: InputMaybe<SortEnumType>;
  minRate?: InputMaybe<SortEnumType>;
  name?: InputMaybe<SortEnumType>;
};

export type ChangePasswordResult = {
  __typename?: 'ChangePasswordResult';
  message: Scalars['String']['output'];
  success: Scalars['Boolean']['output'];
};

export type CodeName = {
  __typename?: 'CodeName';
  code: Scalars['String']['output'];
  name: Scalars['String']['output'];
};

export type CreateDealerResult = {
  __typename?: 'CreateDealerResult';
  dealerCode?: Maybe<Scalars['String']['output']>;
  message: Scalars['String']['output'];
  success: Scalars['Boolean']['output'];
};

export type CreateEventInput = {
  attendees?: InputMaybe<Array<AttendeeInput>>;
  description?: InputMaybe<Scalars['String']['input']>;
  endUtc: Scalars['DateTime']['input'];
  eventTypeId?: InputMaybe<Scalars['Int']['input']>;
  isAllDay: Scalars['Boolean']['input'];
  location?: InputMaybe<Scalars['String']['input']>;
  meetingLink?: InputMaybe<Scalars['String']['input']>;
  recurrence?: InputMaybe<RecurrenceInput>;
  reminders?: InputMaybe<Array<ReminderInput>>;
  showAs?: InputMaybe<Scalars['Int']['input']>;
  startUtc: Scalars['DateTime']['input'];
  status?: InputMaybe<Scalars['Int']['input']>;
  tags?: InputMaybe<Array<EventTagInput>>;
  tasks?: InputMaybe<Array<CalendarTaskInput>>;
  timeZoneId?: InputMaybe<Scalars['String']['input']>;
  title: Scalars['String']['input'];
  visibility?: InputMaybe<Scalars['Int']['input']>;
};

export type Customer = {
  __typename?: 'Customer';
  address?: Maybe<Scalars['String']['output']>;
  address2?: Maybe<Scalars['String']['output']>;
  allowLineDisc: Scalars['Byte']['output'];
  amount: Scalars['Decimal']['output'];
  applicationMethod: Scalars['Int']['output'];
  areaCode?: Maybe<Scalars['String']['output']>;
  balance: Scalars['Decimal']['output'];
  baseCalendarCode?: Maybe<Scalars['String']['output']>;
  billToCustomerNo?: Maybe<Scalars['String']['output']>;
  blockPaymentTolerance: Scalars['Byte']['output'];
  blocked: Scalars['Int']['output'];
  budgetedAmount: Scalars['Decimal']['output'];
  canAttachParties: Scalars['Byte']['output'];
  cashFlowPaymentTermsCode?: Maybe<Scalars['String']['output']>;
  chainName?: Maybe<Scalars['String']['output']>;
  city?: Maybe<Scalars['String']['output']>;
  collectionMethod?: Maybe<Scalars['String']['output']>;
  collectorate?: Maybe<Scalars['String']['output']>;
  combineShipments: Scalars['Byte']['output'];
  contact?: Maybe<Scalars['String']['output']>;
  copySellToAddrToQteFrom: Scalars['Int']['output'];
  countryRegionCode?: Maybe<Scalars['String']['output']>;
  county?: Maybe<Scalars['String']['output']>;
  creditLimitLCY: Scalars['Decimal']['output'];
  cstNo?: Maybe<Scalars['String']['output']>;
  currencyCode?: Maybe<Scalars['String']['output']>;
  customerDiscGroup?: Maybe<Scalars['String']['output']>;
  customerPostingGroup?: Maybe<Scalars['String']['output']>;
  customerPriceGroup?: Maybe<Scalars['String']['output']>;
  dealerCode?: Maybe<Scalars['String']['output']>;
  eCommerceOperator: Scalars['Byte']['output'];
  eMail?: Maybe<Scalars['String']['output']>;
  eccNo?: Maybe<Scalars['String']['output']>;
  exciseBusPostingGroup?: Maybe<Scalars['String']['output']>;
  exportOrDeemedExport: Scalars['Byte']['output'];
  faxNo?: Maybe<Scalars['String']['output']>;
  finChargeTermsCode?: Maybe<Scalars['String']['output']>;
  genBusPostingGroup?: Maybe<Scalars['String']['output']>;
  globalDimension1Code?: Maybe<Scalars['String']['output']>;
  globalDimension2Code?: Maybe<Scalars['String']['output']>;
  groupCategory?: Maybe<Scalars['String']['output']>;
  groupDetails?: Maybe<Scalars['String']['output']>;
  gstBlockStatus: Scalars['Int']['output'];
  gstCustomerType: Scalars['Int']['output'];
  gstLegalName?: Maybe<Scalars['String']['output']>;
  gstRegistrationNo?: Maybe<Scalars['String']['output']>;
  gstRegistrationType: Scalars['Int']['output'];
  gstStatus: Scalars['Int']['output'];
  gstStatusCheck?: Maybe<Scalars['DateTime']['output']>;
  gstSupplyType: Scalars['Int']['output'];
  gstTradeName?: Maybe<Scalars['String']['output']>;
  homePage?: Maybe<Scalars['String']['output']>;
  icPartnerCode?: Maybe<Scalars['String']['output']>;
  invoiceCopies: Scalars['Int']['output'];
  invoiceDiscCode?: Maybe<Scalars['String']['output']>;
  languageCode?: Maybe<Scalars['String']['output']>;
  lastDateModified?: Maybe<Scalars['DateTime']['output']>;
  lastStatementNo: Scalars['Int']['output'];
  locationCode?: Maybe<Scalars['String']['output']>;
  lstNo?: Maybe<Scalars['String']['output']>;
  modeOfCreation: Scalars['Int']['output'];
  name?: Maybe<Scalars['String']['output']>;
  name2?: Maybe<Scalars['String']['output']>;
  nameOnCheck?: Maybe<Scalars['String']['output']>;
  natureOfServices: Scalars['Int']['output'];
  no?: Maybe<Scalars['String']['output']>;
  noCommission: Scalars['Byte']['output'];
  noSeries?: Maybe<Scalars['String']['output']>;
  ourAccountNo?: Maybe<Scalars['String']['output']>;
  panNo?: Maybe<Scalars['String']['output']>;
  panReferenceNo?: Maybe<Scalars['String']['output']>;
  panStatus: Scalars['Int']['output'];
  partnerType: Scalars['Int']['output'];
  paymentMethodCode?: Maybe<Scalars['String']['output']>;
  paymentTermsCode?: Maybe<Scalars['String']['output']>;
  phoneNo?: Maybe<Scalars['String']['output']>;
  picture?: Maybe<Array<Scalars['Byte']['output']>>;
  placeOfExport?: Maybe<Scalars['String']['output']>;
  postCode?: Maybe<Scalars['String']['output']>;
  preferredBankAccount?: Maybe<Scalars['String']['output']>;
  prepayment: Scalars['Decimal']['output'];
  pricesIncludingVAT: Scalars['Byte']['output'];
  primaryContactNo?: Maybe<Scalars['String']['output']>;
  printStatements: Scalars['Byte']['output'];
  priority: Scalars['Int']['output'];
  range?: Maybe<Scalars['String']['output']>;
  reminderTermsCode?: Maybe<Scalars['String']['output']>;
  reserve: Scalars['Int']['output'];
  responsibilityCenter?: Maybe<Scalars['String']['output']>;
  searchName?: Maybe<Scalars['String']['output']>;
  serviceEntityType?: Maybe<Scalars['String']['output']>;
  serviceRespCenter?: Maybe<Scalars['String']['output']>;
  serviceZoneCode?: Maybe<Scalars['String']['output']>;
  shipmentMethodCode?: Maybe<Scalars['String']['output']>;
  shippingAdvice: Scalars['Int']['output'];
  shippingAgentCode?: Maybe<Scalars['String']['output']>;
  shippingAgentServiceCode?: Maybe<Scalars['String']['output']>;
  shippingTime?: Maybe<Scalars['String']['output']>;
  stateCode?: Maybe<Scalars['String']['output']>;
  statisticsGroup: Scalars['Int']['output'];
  status: Scalars['Int']['output'];
  stopAutoCorrectName: Scalars['Byte']['output'];
  stopTCS: Scalars['Byte']['output'];
  structure?: Maybe<Scalars['String']['output']>;
  taxAreaCode?: Maybe<Scalars['String']['output']>;
  taxExemptionNo?: Maybe<Scalars['String']['output']>;
  taxLiable: Scalars['Byte']['output'];
  telexAnswerBack?: Maybe<Scalars['String']['output']>;
  telexNo?: Maybe<Scalars['String']['output']>;
  territoryCode?: Maybe<Scalars['String']['output']>;
  tinNo?: Maybe<Scalars['String']['output']>;
  vatBusPostingGroup?: Maybe<Scalars['String']['output']>;
  vatExempted: Scalars['Byte']['output'];
  vatRegistrationNo?: Maybe<Scalars['String']['output']>;
  zeroRepairs: Scalars['Byte']['output'];
};

export type CustomerFilterInput = {
  address?: InputMaybe<StringOperationFilterInput>;
  address2?: InputMaybe<StringOperationFilterInput>;
  allowLineDisc?: InputMaybe<ByteOperationFilterInput>;
  amount?: InputMaybe<DecimalOperationFilterInput>;
  and?: InputMaybe<Array<CustomerFilterInput>>;
  applicationMethod?: InputMaybe<IntOperationFilterInput>;
  areaCode?: InputMaybe<StringOperationFilterInput>;
  balance?: InputMaybe<DecimalOperationFilterInput>;
  baseCalendarCode?: InputMaybe<StringOperationFilterInput>;
  billToCustomerNo?: InputMaybe<StringOperationFilterInput>;
  blockPaymentTolerance?: InputMaybe<ByteOperationFilterInput>;
  blocked?: InputMaybe<IntOperationFilterInput>;
  budgetedAmount?: InputMaybe<DecimalOperationFilterInput>;
  canAttachParties?: InputMaybe<ByteOperationFilterInput>;
  cashFlowPaymentTermsCode?: InputMaybe<StringOperationFilterInput>;
  chainName?: InputMaybe<StringOperationFilterInput>;
  city?: InputMaybe<StringOperationFilterInput>;
  collectionMethod?: InputMaybe<StringOperationFilterInput>;
  collectorate?: InputMaybe<StringOperationFilterInput>;
  combineShipments?: InputMaybe<ByteOperationFilterInput>;
  contact?: InputMaybe<StringOperationFilterInput>;
  copySellToAddrToQteFrom?: InputMaybe<IntOperationFilterInput>;
  countryRegionCode?: InputMaybe<StringOperationFilterInput>;
  county?: InputMaybe<StringOperationFilterInput>;
  creditLimitLCY?: InputMaybe<DecimalOperationFilterInput>;
  cstNo?: InputMaybe<StringOperationFilterInput>;
  currencyCode?: InputMaybe<StringOperationFilterInput>;
  customerDiscGroup?: InputMaybe<StringOperationFilterInput>;
  customerPostingGroup?: InputMaybe<StringOperationFilterInput>;
  customerPriceGroup?: InputMaybe<StringOperationFilterInput>;
  dealerCode?: InputMaybe<StringOperationFilterInput>;
  eCommerceOperator?: InputMaybe<ByteOperationFilterInput>;
  eMail?: InputMaybe<StringOperationFilterInput>;
  eccNo?: InputMaybe<StringOperationFilterInput>;
  exciseBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  exportOrDeemedExport?: InputMaybe<ByteOperationFilterInput>;
  faxNo?: InputMaybe<StringOperationFilterInput>;
  finChargeTermsCode?: InputMaybe<StringOperationFilterInput>;
  genBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  globalDimension1Code?: InputMaybe<StringOperationFilterInput>;
  globalDimension2Code?: InputMaybe<StringOperationFilterInput>;
  groupCategory?: InputMaybe<StringOperationFilterInput>;
  groupDetails?: InputMaybe<StringOperationFilterInput>;
  gstBlockStatus?: InputMaybe<IntOperationFilterInput>;
  gstCustomerType?: InputMaybe<IntOperationFilterInput>;
  gstLegalName?: InputMaybe<StringOperationFilterInput>;
  gstRegistrationNo?: InputMaybe<StringOperationFilterInput>;
  gstRegistrationType?: InputMaybe<IntOperationFilterInput>;
  gstStatus?: InputMaybe<IntOperationFilterInput>;
  gstStatusCheck?: InputMaybe<DateTimeOperationFilterInput>;
  gstSupplyType?: InputMaybe<IntOperationFilterInput>;
  gstTradeName?: InputMaybe<StringOperationFilterInput>;
  homePage?: InputMaybe<StringOperationFilterInput>;
  icPartnerCode?: InputMaybe<StringOperationFilterInput>;
  invoiceCopies?: InputMaybe<IntOperationFilterInput>;
  invoiceDiscCode?: InputMaybe<StringOperationFilterInput>;
  languageCode?: InputMaybe<StringOperationFilterInput>;
  lastDateModified?: InputMaybe<DateTimeOperationFilterInput>;
  lastStatementNo?: InputMaybe<IntOperationFilterInput>;
  locationCode?: InputMaybe<StringOperationFilterInput>;
  lstNo?: InputMaybe<StringOperationFilterInput>;
  modeOfCreation?: InputMaybe<IntOperationFilterInput>;
  name?: InputMaybe<StringOperationFilterInput>;
  name2?: InputMaybe<StringOperationFilterInput>;
  nameOnCheck?: InputMaybe<StringOperationFilterInput>;
  natureOfServices?: InputMaybe<IntOperationFilterInput>;
  no?: InputMaybe<StringOperationFilterInput>;
  noCommission?: InputMaybe<ByteOperationFilterInput>;
  noSeries?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<CustomerFilterInput>>;
  ourAccountNo?: InputMaybe<StringOperationFilterInput>;
  panNo?: InputMaybe<StringOperationFilterInput>;
  panReferenceNo?: InputMaybe<StringOperationFilterInput>;
  panStatus?: InputMaybe<IntOperationFilterInput>;
  partnerType?: InputMaybe<IntOperationFilterInput>;
  paymentMethodCode?: InputMaybe<StringOperationFilterInput>;
  paymentTermsCode?: InputMaybe<StringOperationFilterInput>;
  phoneNo?: InputMaybe<StringOperationFilterInput>;
  picture?: InputMaybe<ListByteOperationFilterInput>;
  placeOfExport?: InputMaybe<StringOperationFilterInput>;
  postCode?: InputMaybe<StringOperationFilterInput>;
  preferredBankAccount?: InputMaybe<StringOperationFilterInput>;
  prepayment?: InputMaybe<DecimalOperationFilterInput>;
  pricesIncludingVAT?: InputMaybe<ByteOperationFilterInput>;
  primaryContactNo?: InputMaybe<StringOperationFilterInput>;
  printStatements?: InputMaybe<ByteOperationFilterInput>;
  priority?: InputMaybe<IntOperationFilterInput>;
  range?: InputMaybe<StringOperationFilterInput>;
  reminderTermsCode?: InputMaybe<StringOperationFilterInput>;
  reserve?: InputMaybe<IntOperationFilterInput>;
  responsibilityCenter?: InputMaybe<StringOperationFilterInput>;
  searchName?: InputMaybe<StringOperationFilterInput>;
  serviceEntityType?: InputMaybe<StringOperationFilterInput>;
  serviceRespCenter?: InputMaybe<StringOperationFilterInput>;
  serviceZoneCode?: InputMaybe<StringOperationFilterInput>;
  shipmentMethodCode?: InputMaybe<StringOperationFilterInput>;
  shippingAdvice?: InputMaybe<IntOperationFilterInput>;
  shippingAgentCode?: InputMaybe<StringOperationFilterInput>;
  shippingAgentServiceCode?: InputMaybe<StringOperationFilterInput>;
  shippingTime?: InputMaybe<StringOperationFilterInput>;
  stateCode?: InputMaybe<StringOperationFilterInput>;
  statisticsGroup?: InputMaybe<IntOperationFilterInput>;
  status?: InputMaybe<IntOperationFilterInput>;
  stopAutoCorrectName?: InputMaybe<ByteOperationFilterInput>;
  stopTCS?: InputMaybe<ByteOperationFilterInput>;
  structure?: InputMaybe<StringOperationFilterInput>;
  taxAreaCode?: InputMaybe<StringOperationFilterInput>;
  taxExemptionNo?: InputMaybe<StringOperationFilterInput>;
  taxLiable?: InputMaybe<ByteOperationFilterInput>;
  telexAnswerBack?: InputMaybe<StringOperationFilterInput>;
  telexNo?: InputMaybe<StringOperationFilterInput>;
  territoryCode?: InputMaybe<StringOperationFilterInput>;
  tinNo?: InputMaybe<StringOperationFilterInput>;
  vatBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  vatExempted?: InputMaybe<ByteOperationFilterInput>;
  vatRegistrationNo?: InputMaybe<StringOperationFilterInput>;
  zeroRepairs?: InputMaybe<ByteOperationFilterInput>;
};

export type CustomerSortInput = {
  address?: InputMaybe<SortEnumType>;
  address2?: InputMaybe<SortEnumType>;
  allowLineDisc?: InputMaybe<SortEnumType>;
  amount?: InputMaybe<SortEnumType>;
  applicationMethod?: InputMaybe<SortEnumType>;
  areaCode?: InputMaybe<SortEnumType>;
  balance?: InputMaybe<SortEnumType>;
  baseCalendarCode?: InputMaybe<SortEnumType>;
  billToCustomerNo?: InputMaybe<SortEnumType>;
  blockPaymentTolerance?: InputMaybe<SortEnumType>;
  blocked?: InputMaybe<SortEnumType>;
  budgetedAmount?: InputMaybe<SortEnumType>;
  canAttachParties?: InputMaybe<SortEnumType>;
  cashFlowPaymentTermsCode?: InputMaybe<SortEnumType>;
  chainName?: InputMaybe<SortEnumType>;
  city?: InputMaybe<SortEnumType>;
  collectionMethod?: InputMaybe<SortEnumType>;
  collectorate?: InputMaybe<SortEnumType>;
  combineShipments?: InputMaybe<SortEnumType>;
  contact?: InputMaybe<SortEnumType>;
  copySellToAddrToQteFrom?: InputMaybe<SortEnumType>;
  countryRegionCode?: InputMaybe<SortEnumType>;
  county?: InputMaybe<SortEnumType>;
  creditLimitLCY?: InputMaybe<SortEnumType>;
  cstNo?: InputMaybe<SortEnumType>;
  currencyCode?: InputMaybe<SortEnumType>;
  customerDiscGroup?: InputMaybe<SortEnumType>;
  customerPostingGroup?: InputMaybe<SortEnumType>;
  customerPriceGroup?: InputMaybe<SortEnumType>;
  dealerCode?: InputMaybe<SortEnumType>;
  eCommerceOperator?: InputMaybe<SortEnumType>;
  eMail?: InputMaybe<SortEnumType>;
  eccNo?: InputMaybe<SortEnumType>;
  exciseBusPostingGroup?: InputMaybe<SortEnumType>;
  exportOrDeemedExport?: InputMaybe<SortEnumType>;
  faxNo?: InputMaybe<SortEnumType>;
  finChargeTermsCode?: InputMaybe<SortEnumType>;
  genBusPostingGroup?: InputMaybe<SortEnumType>;
  globalDimension1Code?: InputMaybe<SortEnumType>;
  globalDimension2Code?: InputMaybe<SortEnumType>;
  groupCategory?: InputMaybe<SortEnumType>;
  groupDetails?: InputMaybe<SortEnumType>;
  gstBlockStatus?: InputMaybe<SortEnumType>;
  gstCustomerType?: InputMaybe<SortEnumType>;
  gstLegalName?: InputMaybe<SortEnumType>;
  gstRegistrationNo?: InputMaybe<SortEnumType>;
  gstRegistrationType?: InputMaybe<SortEnumType>;
  gstStatus?: InputMaybe<SortEnumType>;
  gstStatusCheck?: InputMaybe<SortEnumType>;
  gstSupplyType?: InputMaybe<SortEnumType>;
  gstTradeName?: InputMaybe<SortEnumType>;
  homePage?: InputMaybe<SortEnumType>;
  icPartnerCode?: InputMaybe<SortEnumType>;
  invoiceCopies?: InputMaybe<SortEnumType>;
  invoiceDiscCode?: InputMaybe<SortEnumType>;
  languageCode?: InputMaybe<SortEnumType>;
  lastDateModified?: InputMaybe<SortEnumType>;
  lastStatementNo?: InputMaybe<SortEnumType>;
  locationCode?: InputMaybe<SortEnumType>;
  lstNo?: InputMaybe<SortEnumType>;
  modeOfCreation?: InputMaybe<SortEnumType>;
  name?: InputMaybe<SortEnumType>;
  name2?: InputMaybe<SortEnumType>;
  nameOnCheck?: InputMaybe<SortEnumType>;
  natureOfServices?: InputMaybe<SortEnumType>;
  no?: InputMaybe<SortEnumType>;
  noCommission?: InputMaybe<SortEnumType>;
  noSeries?: InputMaybe<SortEnumType>;
  ourAccountNo?: InputMaybe<SortEnumType>;
  panNo?: InputMaybe<SortEnumType>;
  panReferenceNo?: InputMaybe<SortEnumType>;
  panStatus?: InputMaybe<SortEnumType>;
  partnerType?: InputMaybe<SortEnumType>;
  paymentMethodCode?: InputMaybe<SortEnumType>;
  paymentTermsCode?: InputMaybe<SortEnumType>;
  phoneNo?: InputMaybe<SortEnumType>;
  placeOfExport?: InputMaybe<SortEnumType>;
  postCode?: InputMaybe<SortEnumType>;
  preferredBankAccount?: InputMaybe<SortEnumType>;
  prepayment?: InputMaybe<SortEnumType>;
  pricesIncludingVAT?: InputMaybe<SortEnumType>;
  primaryContactNo?: InputMaybe<SortEnumType>;
  printStatements?: InputMaybe<SortEnumType>;
  priority?: InputMaybe<SortEnumType>;
  range?: InputMaybe<SortEnumType>;
  reminderTermsCode?: InputMaybe<SortEnumType>;
  reserve?: InputMaybe<SortEnumType>;
  responsibilityCenter?: InputMaybe<SortEnumType>;
  searchName?: InputMaybe<SortEnumType>;
  serviceEntityType?: InputMaybe<SortEnumType>;
  serviceRespCenter?: InputMaybe<SortEnumType>;
  serviceZoneCode?: InputMaybe<SortEnumType>;
  shipmentMethodCode?: InputMaybe<SortEnumType>;
  shippingAdvice?: InputMaybe<SortEnumType>;
  shippingAgentCode?: InputMaybe<SortEnumType>;
  shippingAgentServiceCode?: InputMaybe<SortEnumType>;
  shippingTime?: InputMaybe<SortEnumType>;
  stateCode?: InputMaybe<SortEnumType>;
  statisticsGroup?: InputMaybe<SortEnumType>;
  status?: InputMaybe<SortEnumType>;
  stopAutoCorrectName?: InputMaybe<SortEnumType>;
  stopTCS?: InputMaybe<SortEnumType>;
  structure?: InputMaybe<SortEnumType>;
  taxAreaCode?: InputMaybe<SortEnumType>;
  taxExemptionNo?: InputMaybe<SortEnumType>;
  taxLiable?: InputMaybe<SortEnumType>;
  telexAnswerBack?: InputMaybe<SortEnumType>;
  telexNo?: InputMaybe<SortEnumType>;
  territoryCode?: InputMaybe<SortEnumType>;
  tinNo?: InputMaybe<SortEnumType>;
  vatBusPostingGroup?: InputMaybe<SortEnumType>;
  vatExempted?: InputMaybe<SortEnumType>;
  vatRegistrationNo?: InputMaybe<SortEnumType>;
  zeroRepairs?: InputMaybe<SortEnumType>;
};

export type DateTimeOperationFilterInput = {
  eq?: InputMaybe<Scalars['DateTime']['input']>;
  gt?: InputMaybe<Scalars['DateTime']['input']>;
  gte?: InputMaybe<Scalars['DateTime']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['DateTime']['input']>>>;
  lt?: InputMaybe<Scalars['DateTime']['input']>;
  lte?: InputMaybe<Scalars['DateTime']['input']>;
  neq?: InputMaybe<Scalars['DateTime']['input']>;
  ngt?: InputMaybe<Scalars['DateTime']['input']>;
  ngte?: InputMaybe<Scalars['DateTime']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['DateTime']['input']>>>;
  nlt?: InputMaybe<Scalars['DateTime']['input']>;
  nlte?: InputMaybe<Scalars['DateTime']['input']>;
};

export type DecimalOperationFilterInput = {
  eq?: InputMaybe<Scalars['Decimal']['input']>;
  gt?: InputMaybe<Scalars['Decimal']['input']>;
  gte?: InputMaybe<Scalars['Decimal']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['Decimal']['input']>>>;
  lt?: InputMaybe<Scalars['Decimal']['input']>;
  lte?: InputMaybe<Scalars['Decimal']['input']>;
  neq?: InputMaybe<Scalars['Decimal']['input']>;
  ngt?: InputMaybe<Scalars['Decimal']['input']>;
  ngte?: InputMaybe<Scalars['Decimal']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['Decimal']['input']>>>;
  nlt?: InputMaybe<Scalars['Decimal']['input']>;
  nlte?: InputMaybe<Scalars['Decimal']['input']>;
};

export type DispatchOrder = {
  __typename?: 'DispatchOrder';
  date: Scalars['String']['output'];
  destination: Scalars['String']['output'];
  mobileNo: Scalars['String']['output'];
  no: Scalars['String']['output'];
  status: Scalars['String']['output'];
  tyres: Scalars['Int']['output'];
  vehicleNo: Scalars['String']['output'];
};

export type DocDetail = {
  __typename?: 'DocDetail';
  dt: Scalars['String']['output'];
  no: Scalars['String']['output'];
  typ: Scalars['String']['output'];
};

export type DocumentDto = {
  __typename?: 'DocumentDto';
  amount: Scalars['Decimal']['output'];
  customerNo?: Maybe<Scalars['String']['output']>;
  date?: Maybe<Scalars['DateTime']['output']>;
  name?: Maybe<Scalars['String']['output']>;
  no?: Maybe<Scalars['String']['output']>;
};

export type EInvoiceCandidate = {
  __typename?: 'EInvoiceCandidate';
  address1: Scalars['String']['output'];
  address2?: Maybe<Scalars['String']['output']>;
  buyerAddr1: Scalars['String']['output'];
  buyerAddr2?: Maybe<Scalars['String']['output']>;
  buyerCity: Scalars['String']['output'];
  buyerGstin: Scalars['String']['output'];
  buyerName: Scalars['String']['output'];
  buyerPincode: Scalars['String']['output'];
  buyerState: Scalars['String']['output'];
  cgstValue: Scalars['Decimal']['output'];
  city: Scalars['String']['output'];
  documentNo: Scalars['String']['output'];
  documentType: Scalars['String']['output'];
  eInvSkip: Scalars['Boolean']['output'];
  existingIrn?: Maybe<Scalars['String']['output']>;
  gstTradeName: Scalars['String']['output'];
  gstin: Scalars['String']['output'];
  igstValue: Scalars['Decimal']['output'];
  lines: Array<InvoiceItem>;
  othChrg: Scalars['Decimal']['output'];
  password: Scalars['String']['output'];
  pincode: Scalars['String']['output'];
  postingDate: Scalars['String']['output'];
  respCenter: Scalars['String']['output'];
  rndOffAmt: Scalars['Decimal']['output'];
  sgstValue: Scalars['Decimal']['output'];
  stateCode: Scalars['String']['output'];
  toPayload: EInvoicePayload;
  totInvValue: Scalars['Decimal']['output'];
  totalValue: Scalars['Decimal']['output'];
  userName: Scalars['String']['output'];
};

export type EInvoicePayload = {
  __typename?: 'EInvoicePayload';
  buyerDtls?: Maybe<BuyerDetail>;
  docDtls?: Maybe<DocDetail>;
  itemList: Array<InvoiceItem>;
  sellerDtls?: Maybe<SellerDetail>;
  shipDtls?: Maybe<ShipDetail>;
  tranDtls?: Maybe<TransDetail>;
  valDtls?: Maybe<ValueDetails>;
  version: Scalars['String']['output'];
};

export type EntityBalance = {
  __typename?: 'EntityBalance';
  balance: Scalars['Decimal']['output'];
  code: Scalars['String']['output'];
  product: Scalars['String']['output'];
};

export type EventAttendeeDto = {
  __typename?: 'EventAttendeeDto';
  email?: Maybe<Scalars['String']['output']>;
  id: Scalars['UUID']['output'];
  isRequired: Scalars['Boolean']['output'];
  respondedAt?: Maybe<Scalars['DateTime']['output']>;
  response: Scalars['Int']['output'];
  userId: Scalars['String']['output'];
};

export type EventTagDto = {
  __typename?: 'EventTagDto';
  displayName?: Maybe<Scalars['String']['output']>;
  tagKey: Scalars['String']['output'];
  tagType: EventTagType;
};

export type EventTagInput = {
  tagKey: Scalars['String']['input'];
  tagType: EventTagType;
};

export enum EventTagType {
  Customer = 'CUSTOMER',
  Topic = 'TOPIC',
  User = 'USER',
  Vendor = 'VENDOR'
}

export type EventTypeDto = {
  __typename?: 'EventTypeDto';
  color?: Maybe<Scalars['String']['output']>;
  icon?: Maybe<Scalars['String']['output']>;
  id: Scalars['Int']['output'];
  name: Scalars['String']['output'];
  sortOrder: Scalars['Int']['output'];
};

export type FetchParamsInput = {
  areas: Array<Scalars['String']['input']>;
  from: Scalars['String']['input'];
  nos: Array<Scalars['String']['input']>;
  regions: Array<Scalars['String']['input']>;
  reportName: Scalars['String']['input'];
  respCenters: Array<Scalars['String']['input']>;
  to: Scalars['String']['input'];
  type: Scalars['String']['input'];
  userCode: Scalars['String']['input'];
  userDepartment: Scalars['String']['input'];
  userSpecialToken: Scalars['String']['input'];
  userType: Scalars['String']['input'];
  view: Scalars['String']['input'];
};

export type FreeBusyDto = {
  __typename?: 'FreeBusyDto';
  busy: Array<FreeBusySlotDto>;
  userId: Scalars['String']['output'];
};

export type FreeBusySlotDto = {
  __typename?: 'FreeBusySlotDto';
  endUtc: Scalars['DateTime']['output'];
  eventId?: Maybe<Scalars['UUID']['output']>;
  startUtc: Scalars['DateTime']['output'];
  title?: Maybe<Scalars['String']['output']>;
};

export type GroupDetails = {
  __typename?: 'GroupDetails';
  category?: Maybe<Scalars['String']['output']>;
  code?: Maybe<Scalars['String']['output']>;
  extraValue?: Maybe<Scalars['String']['output']>;
  name?: Maybe<Scalars['String']['output']>;
  value?: Maybe<Scalars['String']['output']>;
};

export type IntOperationFilterInput = {
  eq?: InputMaybe<Scalars['Int']['input']>;
  gt?: InputMaybe<Scalars['Int']['input']>;
  gte?: InputMaybe<Scalars['Int']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['Int']['input']>>>;
  lt?: InputMaybe<Scalars['Int']['input']>;
  lte?: InputMaybe<Scalars['Int']['input']>;
  neq?: InputMaybe<Scalars['Int']['input']>;
  ngt?: InputMaybe<Scalars['Int']['input']>;
  ngte?: InputMaybe<Scalars['Int']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['Int']['input']>>>;
  nlt?: InputMaybe<Scalars['Int']['input']>;
  nlte?: InputMaybe<Scalars['Int']['input']>;
};

export type InvoiceItem = {
  __typename?: 'InvoiceItem';
  assAmt: Scalars['Decimal']['output'];
  cgstAmt: Scalars['Decimal']['output'];
  discount: Scalars['Decimal']['output'];
  gstRt: Scalars['Decimal']['output'];
  hsnCd: Scalars['String']['output'];
  igstAmt: Scalars['Decimal']['output'];
  isServc: Scalars['String']['output'];
  otherchrg: Scalars['Decimal']['output'];
  prdDesc?: Maybe<Scalars['String']['output']>;
  qty: Scalars['Decimal']['output'];
  sgstAmt: Scalars['Decimal']['output'];
  slNo: Scalars['String']['output'];
  totAmt: Scalars['Decimal']['output'];
  totItemVal: Scalars['Decimal']['output'];
  unit?: Maybe<Scalars['String']['output']>;
  unitPrice: Scalars['Decimal']['output'];
};

export type KillSessionResult = {
  __typename?: 'KillSessionResult';
  message: Scalars['String']['output'];
  success: Scalars['Boolean']['output'];
};

export type KillSessionsByUserResult = {
  __typename?: 'KillSessionsByUserResult';
  killedCount: Scalars['Int']['output'];
  success: Scalars['Boolean']['output'];
};

export type ListByteOperationFilterInput = {
  all?: InputMaybe<ByteOperationFilterInput>;
  any?: InputMaybe<Scalars['Boolean']['input']>;
  none?: InputMaybe<ByteOperationFilterInput>;
  some?: InputMaybe<ByteOperationFilterInput>;
};

export type LoginResult = {
  __typename?: 'LoginResult';
  locations?: Maybe<Array<UserLocation>>;
  menus?: Maybe<Array<Menu>>;
  message?: Maybe<Scalars['String']['output']>;
  requirePasswordChange: Scalars['Boolean']['output'];
  requirePasswordChangeReason?: Maybe<Scalars['String']['output']>;
  success: Scalars['Boolean']['output'];
  token?: Maybe<Scalars['String']['output']>;
  user?: Maybe<LoginUser>;
};

export type LoginUser = {
  __typename?: 'LoginUser';
  avatar: Scalars['Int']['output'];
  department: Scalars['String']['output'];
  entityCode: Scalars['String']['output'];
  entityType: Scalars['String']['output'];
  fullName: Scalars['String']['output'];
  respCenter: Scalars['String']['output'];
  title: Scalars['String']['output'];
  userId: Scalars['String']['output'];
  userSecurityId: Scalars['UUID']['output'];
  userSpecialToken: Scalars['String']['output'];
  userType: Scalars['String']['output'];
  workDate: Scalars['DateTime']['output'];
};

export type Menu = {
  __typename?: 'Menu';
  icon: Scalars['String']['output'];
  label: Scalars['String']['output'];
  subMenus: Array<SubMenu>;
};

export type MenuItem = {
  __typename?: 'MenuItem';
  action: Scalars['String']['output'];
  code: Scalars['String']['output'];
  icon: Scalars['String']['output'];
  label: Scalars['String']['output'];
  options: Scalars['String']['output'];
  order: Scalars['Int']['output'];
};

export type Mutation = {
  __typename?: 'Mutation';
  changePassword: ChangePasswordResult;
  createCalendarEvent?: Maybe<CalendarEventDto>;
  createDealer: CreateDealerResult;
  createProductionVendor: Scalars['String']['output'];
  deleteCalendarEvent: Scalars['Boolean']['output'];
  deleteProductionProcurementOrder: Scalars['Int']['output'];
  deleteProductionProcurementOrderLine: Scalars['Int']['output'];
  forgotPassword: ChangePasswordResult;
  generateProductionGRAs: Scalars['String']['output'];
  insertProductionCasingItems: MutationResult;
  insertProductionProcurementOrderLine: Scalars['Int']['output'];
  killSession: KillSessionResult;
  killSessionsByUser: KillSessionsByUserResult;
  login: LoginResult;
  markAllNotificationsAsRead: Scalars['Boolean']['output'];
  markNotificationAsRead: Scalars['Boolean']['output'];
  newProductionProcShipNo: Scalars['String']['output'];
  newProductionProcurementOrder: Scalars['String']['output'];
  printDocuments?: Maybe<Scalars['String']['output']>;
  resetPassword: ResetPasswordResult;
  respondToInvite: Scalars['Boolean']['output'];
  saveDealer: MutationResult;
  setNotificationPreference: Scalars['Boolean']['output'];
  setProfile: SetProfileResult;
  shareCalendar: Scalars['Boolean']['output'];
  snoozeReminder: Scalars['Boolean']['output'];
  toggleCalendarTaskStatus: Scalars['Boolean']['output'];
  updateCalendarEvent?: Maybe<CalendarEventDto>;
  updateEcomileNewNumberLine: Scalars['Int']['output'];
  updateProductionCasing: MutationResult;
  updateProductionProcOrdLineDispatch: Scalars['Int']['output'];
  updateProductionProcOrdLineDispatchSingle: Scalars['Int']['output'];
  updateProductionProcOrdLineDrop: Scalars['Int']['output'];
  updateProductionProcOrdLineReceipt: Scalars['Int']['output'];
  updateProductionProcOrdLineRemove: Scalars['Int']['output'];
  updateProductionProcurementOrder: Scalars['Int']['output'];
  updateProductionProcurementOrderLine: Scalars['Int']['output'];
  updateProductionVendor: MutationResult;
};


export type MutationChangePasswordArgs = {
  newPassword: Scalars['String']['input'];
  oldPassword?: InputMaybe<Scalars['String']['input']>;
  securityPin?: InputMaybe<Scalars['Int']['input']>;
  userId: Scalars['String']['input'];
};


export type MutationCreateCalendarEventArgs = {
  input: CreateEventInput;
};


export type MutationCreateDealerArgs = {
  customerNo: Scalars['String']['input'];
};


export type MutationCreateProductionVendorArgs = {
  param: FetchParamsInput;
};


export type MutationDeleteCalendarEventArgs = {
  deleteScope?: Scalars['Int']['input'];
  eventId: Scalars['UUID']['input'];
  occurrenceStartUtc?: InputMaybe<Scalars['DateTime']['input']>;
  soft?: Scalars['Boolean']['input'];
};


export type MutationDeleteProductionProcurementOrderArgs = {
  order: OrderInfoInput;
};


export type MutationDeleteProductionProcurementOrderLineArgs = {
  order: OrderLineInput;
};


export type MutationForgotPasswordArgs = {
  newPassword: Scalars['String']['input'];
  securityPin: Scalars['Int']['input'];
  username: Scalars['String']['input'];
};


export type MutationGenerateProductionGrAsArgs = {
  param: FetchParamsInput;
};


export type MutationInsertProductionCasingItemsArgs = {
  casingItems: Array<CasingItemInput>;
};


export type MutationInsertProductionProcurementOrderLineArgs = {
  order: OrderLineInput;
};


export type MutationKillSessionArgs = {
  sessionId: Scalars['String']['input'];
};


export type MutationKillSessionsByUserArgs = {
  userId: Scalars['String']['input'];
};


export type MutationLoginArgs = {
  password: Scalars['String']['input'];
  platform?: InputMaybe<Scalars['String']['input']>;
  username: Scalars['String']['input'];
};


export type MutationMarkNotificationAsReadArgs = {
  notificationId: Scalars['UUID']['input'];
};


export type MutationNewProductionProcShipNoArgs = {
  param: FetchParamsInput;
};


export type MutationNewProductionProcurementOrderArgs = {
  param: FetchParamsInput;
};


export type MutationPrintDocumentsArgs = {
  parameters: SalesReportParamsInput;
};


export type MutationResetPasswordArgs = {
  userId: Scalars['String']['input'];
};


export type MutationRespondToInviteArgs = {
  eventId: Scalars['UUID']['input'];
  response: Scalars['Int']['input'];
};


export type MutationSaveDealerArgs = {
  input: SaveDealerInput;
};


export type MutationSetNotificationPreferenceArgs = {
  input: NotificationPreferenceDtoInput;
};


export type MutationSetProfileArgs = {
  input: ProfileUpdateInput;
  userId: Scalars['String']['input'];
};


export type MutationShareCalendarArgs = {
  permission: Scalars['Int']['input'];
  sharedWithUserId: Scalars['String']['input'];
};


export type MutationSnoozeReminderArgs = {
  reminderId: Scalars['UUID']['input'];
  snoozeUntilUtc: Scalars['DateTime']['input'];
};


export type MutationToggleCalendarTaskStatusArgs = {
  isCompleted: Scalars['Boolean']['input'];
  taskId: Scalars['UUID']['input'];
};


export type MutationUpdateCalendarEventArgs = {
  eventId: Scalars['UUID']['input'];
  input: UpdateEventInput;
  occurrenceStartUtc?: InputMaybe<Scalars['DateTime']['input']>;
  updateScope?: Scalars['Int']['input'];
};


export type MutationUpdateEcomileNewNumberLineArgs = {
  line: OrderLineDispatchInput;
};


export type MutationUpdateProductionCasingArgs = {
  param: FetchParamsInput;
};


export type MutationUpdateProductionProcOrdLineDispatchArgs = {
  lines: Array<OrderLineDispatchInput>;
};


export type MutationUpdateProductionProcOrdLineDispatchSingleArgs = {
  line: OrderLineDispatchInput;
};


export type MutationUpdateProductionProcOrdLineDropArgs = {
  lines: Array<OrderLineDispatchInput>;
};


export type MutationUpdateProductionProcOrdLineReceiptArgs = {
  lines: Array<OrderLineDispatchInput>;
};


export type MutationUpdateProductionProcOrdLineRemoveArgs = {
  lines: Array<OrderLineDispatchInput>;
};


export type MutationUpdateProductionProcurementOrderArgs = {
  order: OrderInfoInput;
};


export type MutationUpdateProductionProcurementOrderLineArgs = {
  order: OrderLineInput;
};


export type MutationUpdateProductionVendorArgs = {
  param: VendorModelInput;
};

export type MutationResult = {
  __typename?: 'MutationResult';
  message: Scalars['String']['output'];
  success: Scalars['Boolean']['output'];
};

/** A connection to a list of items. */
export type MyAreasConnection = {
  __typename?: 'MyAreasConnection';
  /** A list of edges. */
  edges?: Maybe<Array<MyAreasEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<Area>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type MyAreasEdge = {
  __typename?: 'MyAreasEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: Area;
};

/** A connection to a list of items. */
export type MyCustomersConnection = {
  __typename?: 'MyCustomersConnection';
  /** A list of edges. */
  edges?: Maybe<Array<MyCustomersEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<Customer>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type MyCustomersEdge = {
  __typename?: 'MyCustomersEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: Customer;
};

/** A connection to a list of items. */
export type MyDealersConnection = {
  __typename?: 'MyDealersConnection';
  /** A list of edges. */
  edges?: Maybe<Array<MyDealersEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<SalespersonPurchaser>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type MyDealersEdge = {
  __typename?: 'MyDealersEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: SalespersonPurchaser;
};

/** A connection to a list of items. */
export type MyRegionsConnection = {
  __typename?: 'MyRegionsConnection';
  /** A list of edges. */
  edges?: Maybe<Array<MyRegionsEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<Territory>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type MyRegionsEdge = {
  __typename?: 'MyRegionsEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: Territory;
};

/** A connection to a list of items. */
export type MyRespCentersConnection = {
  __typename?: 'MyRespCentersConnection';
  /** A list of edges. */
  edges?: Maybe<Array<MyRespCentersEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<ResponsibilityCenter>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type MyRespCentersEdge = {
  __typename?: 'MyRespCentersEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: ResponsibilityCenter;
};

/** A connection to a list of items. */
export type MyTransactionsConnection = {
  __typename?: 'MyTransactionsConnection';
  /** A list of edges. */
  edges?: Maybe<Array<MyTransactionsEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<AccountTransaction>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type MyTransactionsEdge = {
  __typename?: 'MyTransactionsEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: AccountTransaction;
};

/** A connection to a list of items. */
export type MyVehiclesConnection = {
  __typename?: 'MyVehiclesConnection';
  /** A list of edges. */
  edges?: Maybe<Array<MyVehiclesEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<Vehicles>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type MyVehiclesEdge = {
  __typename?: 'MyVehiclesEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: Vehicles;
};

/** A connection to a list of items. */
export type MyVendorsConnection = {
  __typename?: 'MyVendorsConnection';
  /** A list of edges. */
  edges?: Maybe<Array<MyVendorsEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<Vendor>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type MyVendorsEdge = {
  __typename?: 'MyVendorsEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: Vendor;
};

export type Notification = {
  __typename?: 'Notification';
  createdAt: Scalars['DateTime']['output'];
  id: Scalars['UUID']['output'];
  isRead: Scalars['Boolean']['output'];
  link?: Maybe<Scalars['String']['output']>;
  message: Scalars['String']['output'];
  title: Scalars['String']['output'];
  type: NotificationType;
  userId: Scalars['String']['output'];
};

export type NotificationPreferenceDto = {
  __typename?: 'NotificationPreferenceDto';
  channel: Scalars['Int']['output'];
  defaultMinutesBefore: Scalars['Int']['output'];
  emailEnabled: Scalars['Boolean']['output'];
  pushEnabled: Scalars['Boolean']['output'];
};

export type NotificationPreferenceDtoInput = {
  channel: Scalars['Int']['input'];
  defaultMinutesBefore: Scalars['Int']['input'];
  emailEnabled: Scalars['Boolean']['input'];
  pushEnabled: Scalars['Boolean']['input'];
};

export enum NotificationType {
  Error = 'ERROR',
  Info = 'INFO',
  Success = 'SUCCESS',
  Warning = 'WARNING'
}

export type OrderInfo = {
  __typename?: 'OrderInfo';
  amount: Scalars['Decimal']['output'];
  date: Scalars['String']['output'];
  location: Scalars['String']['output'];
  manager: Scalars['String']['output'];
  managerCode: Scalars['String']['output'];
  orderNo: Scalars['String']['output'];
  qty: Scalars['Int']['output'];
  respCenter: Scalars['String']['output'];
  status: Scalars['Int']['output'];
  supplier: Scalars['String']['output'];
  supplierCode: Scalars['String']['output'];
};

export type OrderInfoInput = {
  amount: Scalars['Decimal']['input'];
  date: Scalars['String']['input'];
  location: Scalars['String']['input'];
  manager: Scalars['String']['input'];
  managerCode: Scalars['String']['input'];
  orderNo: Scalars['String']['input'];
  qty: Scalars['Int']['input'];
  respCenter: Scalars['String']['input'];
  status: Scalars['Int']['input'];
  supplier: Scalars['String']['input'];
  supplierCode: Scalars['String']['input'];
};

export type OrderLine = {
  __typename?: 'OrderLine';
  amount: Scalars['Decimal']['output'];
  inspection: Scalars['String']['output'];
  inspector: Scalars['String']['output'];
  inspectorCode: Scalars['String']['output'];
  itemNo: Scalars['String']['output'];
  lineNo: Scalars['Int']['output'];
  make: Scalars['String']['output'];
  no: Scalars['String']['output'];
  serialNo: Scalars['String']['output'];
  sortNo: Scalars['String']['output'];
  subMake: Scalars['String']['output'];
  vendorNo: Scalars['String']['output'];
};

export type OrderLineDispatch = {
  __typename?: 'OrderLineDispatch';
  button: Scalars['String']['output'];
  date: Scalars['String']['output'];
  dispatchDate: Scalars['String']['output'];
  dispatchDestination: Scalars['String']['output'];
  dispatchMobileNo: Scalars['String']['output'];
  dispatchOrderNo: Scalars['String']['output'];
  dispatchTransporter: Scalars['String']['output'];
  dispatchVehicleNo: Scalars['String']['output'];
  factInspection: Scalars['String']['output'];
  factInspector: Scalars['String']['output'];
  factInspectorFinal: Scalars['String']['output'];
  inspection: Scalars['String']['output'];
  inspector: Scalars['String']['output'];
  lineNo: Scalars['Int']['output'];
  location: Scalars['String']['output'];
  make: Scalars['String']['output'];
  model: Scalars['String']['output'];
  newSerialNo: Scalars['String']['output'];
  no: Scalars['String']['output'];
  orderNo: Scalars['String']['output'];
  orderStatus: Scalars['String']['output'];
  rejectionReason: Scalars['String']['output'];
  remark: Scalars['String']['output'];
  serialNo: Scalars['String']['output'];
  sortNo: Scalars['String']['output'];
  supplier: Scalars['String']['output'];
};

export type OrderLineDispatchInput = {
  button: Scalars['String']['input'];
  date: Scalars['String']['input'];
  dispatchDate: Scalars['String']['input'];
  dispatchDestination: Scalars['String']['input'];
  dispatchMobileNo: Scalars['String']['input'];
  dispatchOrderNo: Scalars['String']['input'];
  dispatchTransporter: Scalars['String']['input'];
  dispatchVehicleNo: Scalars['String']['input'];
  factInspection: Scalars['String']['input'];
  factInspector: Scalars['String']['input'];
  factInspectorFinal: Scalars['String']['input'];
  inspection: Scalars['String']['input'];
  inspector: Scalars['String']['input'];
  lineNo: Scalars['Int']['input'];
  location: Scalars['String']['input'];
  make: Scalars['String']['input'];
  model: Scalars['String']['input'];
  newSerialNo: Scalars['String']['input'];
  no: Scalars['String']['input'];
  orderNo: Scalars['String']['input'];
  orderStatus: Scalars['String']['input'];
  rejectionReason: Scalars['String']['input'];
  remark: Scalars['String']['input'];
  serialNo: Scalars['String']['input'];
  sortNo: Scalars['String']['input'];
  supplier: Scalars['String']['input'];
};

export type OrderLineInput = {
  amount: Scalars['Decimal']['input'];
  inspection: Scalars['String']['input'];
  inspector: Scalars['String']['input'];
  inspectorCode: Scalars['String']['input'];
  itemNo: Scalars['String']['input'];
  lineNo: Scalars['Int']['input'];
  make: Scalars['String']['input'];
  no: Scalars['String']['input'];
  serialNo: Scalars['String']['input'];
  sortNo: Scalars['String']['input'];
  subMake: Scalars['String']['input'];
  vendorNo: Scalars['String']['input'];
};

/** Information about pagination in a connection. */
export type PageInfo = {
  __typename?: 'PageInfo';
  /** When paginating forwards, the cursor to continue. */
  endCursor?: Maybe<Scalars['String']['output']>;
  /** Indicates whether more edges exist following the set defined by the clients arguments. */
  hasNextPage: Scalars['Boolean']['output'];
  /** Indicates whether more edges exist prior the set defined by the clients arguments. */
  hasPreviousPage: Scalars['Boolean']['output'];
  /** When paginating backwards, the cursor to continue. */
  startCursor?: Maybe<Scalars['String']['output']>;
};

export type PostCode = {
  __typename?: 'PostCode';
  city?: Maybe<Scalars['String']['output']>;
  code?: Maybe<Scalars['String']['output']>;
  countryRegionCode?: Maybe<Scalars['String']['output']>;
  county?: Maybe<Scalars['String']['output']>;
  searchCity?: Maybe<Scalars['String']['output']>;
  stateCode?: Maybe<Scalars['String']['output']>;
};

export type PostCodeFilterInput = {
  and?: InputMaybe<Array<PostCodeFilterInput>>;
  city?: InputMaybe<StringOperationFilterInput>;
  code?: InputMaybe<StringOperationFilterInput>;
  countryRegionCode?: InputMaybe<StringOperationFilterInput>;
  county?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<PostCodeFilterInput>>;
  searchCity?: InputMaybe<StringOperationFilterInput>;
  stateCode?: InputMaybe<StringOperationFilterInput>;
};

export type PostCodeSortInput = {
  city?: InputMaybe<SortEnumType>;
  code?: InputMaybe<SortEnumType>;
  countryRegionCode?: InputMaybe<SortEnumType>;
  county?: InputMaybe<SortEnumType>;
  searchCity?: InputMaybe<SortEnumType>;
  stateCode?: InputMaybe<SortEnumType>;
};

/** A connection to a list of items. */
export type PostCodesConnection = {
  __typename?: 'PostCodesConnection';
  /** A list of edges. */
  edges?: Maybe<Array<PostCodesEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<PostCode>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type PostCodesEdge = {
  __typename?: 'PostCodesEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: PostCode;
};

export type ProcurementNewNumberingDto = {
  __typename?: 'ProcurementNewNumberingDto';
  button: Scalars['String']['output'];
  buyFromVendorNo: Scalars['String']['output'];
  date?: Maybe<Scalars['DateTime']['output']>;
  dispatchDate?: Maybe<Scalars['DateTime']['output']>;
  dispatchDestination: Scalars['String']['output'];
  dispatchMobileNo: Scalars['String']['output'];
  dispatchOrderNo: Scalars['String']['output'];
  dispatchTransporter: Scalars['String']['output'];
  dispatchVehicleNo: Scalars['String']['output'];
  factInspection: Scalars['String']['output'];
  factInspector: Scalars['String']['output'];
  factInspectorCode: Scalars['String']['output'];
  factInspectorFinal: Scalars['String']['output'];
  factInspectorFinalCode: Scalars['String']['output'];
  inspection: Scalars['String']['output'];
  inspector: Scalars['String']['output'];
  inspectorCode: Scalars['String']['output'];
  lineNo: Scalars['Int']['output'];
  location: Scalars['String']['output'];
  make: Scalars['String']['output'];
  model: Scalars['String']['output'];
  newSerialNo: Scalars['String']['output'];
  no: Scalars['String']['output'];
  orderNo: Scalars['String']['output'];
  orderStatus: Scalars['String']['output'];
  rejectionReason: Scalars['String']['output'];
  remark: Scalars['String']['output'];
  serialNo: Scalars['String']['output'];
  sortNo: Scalars['String']['output'];
  supplier: Scalars['String']['output'];
};

export type ProcurementNewNumberingDtoFilterInput = {
  and?: InputMaybe<Array<ProcurementNewNumberingDtoFilterInput>>;
  button?: InputMaybe<StringOperationFilterInput>;
  buyFromVendorNo?: InputMaybe<StringOperationFilterInput>;
  date?: InputMaybe<DateTimeOperationFilterInput>;
  dispatchDate?: InputMaybe<DateTimeOperationFilterInput>;
  dispatchDestination?: InputMaybe<StringOperationFilterInput>;
  dispatchMobileNo?: InputMaybe<StringOperationFilterInput>;
  dispatchOrderNo?: InputMaybe<StringOperationFilterInput>;
  dispatchTransporter?: InputMaybe<StringOperationFilterInput>;
  dispatchVehicleNo?: InputMaybe<StringOperationFilterInput>;
  factInspection?: InputMaybe<StringOperationFilterInput>;
  factInspector?: InputMaybe<StringOperationFilterInput>;
  factInspectorCode?: InputMaybe<StringOperationFilterInput>;
  factInspectorFinal?: InputMaybe<StringOperationFilterInput>;
  factInspectorFinalCode?: InputMaybe<StringOperationFilterInput>;
  inspection?: InputMaybe<StringOperationFilterInput>;
  inspector?: InputMaybe<StringOperationFilterInput>;
  inspectorCode?: InputMaybe<StringOperationFilterInput>;
  lineNo?: InputMaybe<IntOperationFilterInput>;
  location?: InputMaybe<StringOperationFilterInput>;
  make?: InputMaybe<StringOperationFilterInput>;
  model?: InputMaybe<StringOperationFilterInput>;
  newSerialNo?: InputMaybe<StringOperationFilterInput>;
  no?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<ProcurementNewNumberingDtoFilterInput>>;
  orderNo?: InputMaybe<StringOperationFilterInput>;
  orderStatus?: InputMaybe<StringOperationFilterInput>;
  rejectionReason?: InputMaybe<StringOperationFilterInput>;
  remark?: InputMaybe<StringOperationFilterInput>;
  serialNo?: InputMaybe<StringOperationFilterInput>;
  sortNo?: InputMaybe<StringOperationFilterInput>;
  supplier?: InputMaybe<StringOperationFilterInput>;
};

export type ProcurementNewNumberingDtoSortInput = {
  button?: InputMaybe<SortEnumType>;
  buyFromVendorNo?: InputMaybe<SortEnumType>;
  date?: InputMaybe<SortEnumType>;
  dispatchDate?: InputMaybe<SortEnumType>;
  dispatchDestination?: InputMaybe<SortEnumType>;
  dispatchMobileNo?: InputMaybe<SortEnumType>;
  dispatchOrderNo?: InputMaybe<SortEnumType>;
  dispatchTransporter?: InputMaybe<SortEnumType>;
  dispatchVehicleNo?: InputMaybe<SortEnumType>;
  factInspection?: InputMaybe<SortEnumType>;
  factInspector?: InputMaybe<SortEnumType>;
  factInspectorCode?: InputMaybe<SortEnumType>;
  factInspectorFinal?: InputMaybe<SortEnumType>;
  factInspectorFinalCode?: InputMaybe<SortEnumType>;
  inspection?: InputMaybe<SortEnumType>;
  inspector?: InputMaybe<SortEnumType>;
  inspectorCode?: InputMaybe<SortEnumType>;
  lineNo?: InputMaybe<SortEnumType>;
  location?: InputMaybe<SortEnumType>;
  make?: InputMaybe<SortEnumType>;
  model?: InputMaybe<SortEnumType>;
  newSerialNo?: InputMaybe<SortEnumType>;
  no?: InputMaybe<SortEnumType>;
  orderNo?: InputMaybe<SortEnumType>;
  orderStatus?: InputMaybe<SortEnumType>;
  rejectionReason?: InputMaybe<SortEnumType>;
  remark?: InputMaybe<SortEnumType>;
  serialNo?: InputMaybe<SortEnumType>;
  sortNo?: InputMaybe<SortEnumType>;
  supplier?: InputMaybe<SortEnumType>;
};

/** A connection to a list of items. */
export type ProcurementNewNumberingPagedConnection = {
  __typename?: 'ProcurementNewNumberingPagedConnection';
  /** A list of edges. */
  edges?: Maybe<Array<ProcurementNewNumberingPagedEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<ProcurementNewNumberingDto>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type ProcurementNewNumberingPagedEdge = {
  __typename?: 'ProcurementNewNumberingPagedEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: ProcurementNewNumberingDto;
};

/** A connection to a list of items. */
export type ProcurementOrderLinesConnection = {
  __typename?: 'ProcurementOrderLinesConnection';
  /** A list of edges. */
  edges?: Maybe<Array<ProcurementOrderLinesEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<PurchaseLine>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type ProcurementOrderLinesEdge = {
  __typename?: 'ProcurementOrderLinesEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: PurchaseLine;
};

/** A connection to a list of items. */
export type ProcurementOrdersConnection = {
  __typename?: 'ProcurementOrdersConnection';
  /** A list of edges. */
  edges?: Maybe<Array<ProcurementOrdersEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<PurchaseHeader>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type ProcurementOrdersEdge = {
  __typename?: 'ProcurementOrdersEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: PurchaseHeader;
};

export type ProfileResult = {
  __typename?: 'ProfileResult';
  avatar: Scalars['Int']['output'];
  email: Scalars['String']['output'];
  entities: Array<UserEntity>;
  fullName: Scalars['String']['output'];
  lastPasswordChanged: Scalars['DateTime']['output'];
  mobileNo: Scalars['String']['output'];
  securityPIN: Scalars['Int']['output'];
  userId: Scalars['String']['output'];
  userType: Scalars['String']['output'];
};

export type ProfileUpdateInput = {
  avatar?: InputMaybe<Scalars['Int']['input']>;
  email?: InputMaybe<Scalars['String']['input']>;
  fullName?: InputMaybe<Scalars['String']['input']>;
  mobileNo?: InputMaybe<Scalars['String']['input']>;
  securityPIN?: InputMaybe<Scalars['Int']['input']>;
};

export type PurchaseHeader = {
  __typename?: 'PurchaseHeader';
  appliesToDocNo?: Maybe<Scalars['String']['output']>;
  appliesToDocType: Scalars['Int']['output'];
  appliesToID?: Maybe<Scalars['String']['output']>;
  area?: Maybe<Scalars['String']['output']>;
  assesseeCode?: Maybe<Scalars['String']['output']>;
  assignedUserID?: Maybe<Scalars['String']['output']>;
  associatedEnterprises: Scalars['Byte']['output'];
  balAccountNo?: Maybe<Scalars['String']['output']>;
  balAccountType: Scalars['Int']['output'];
  billOfEntryDate?: Maybe<Scalars['DateTime']['output']>;
  billOfEntryNo?: Maybe<Scalars['String']['output']>;
  billOfEntryValue: Scalars['Decimal']['output'];
  buyFromAddress?: Maybe<Scalars['String']['output']>;
  buyFromAddress2?: Maybe<Scalars['String']['output']>;
  buyFromCity?: Maybe<Scalars['String']['output']>;
  buyFromContact?: Maybe<Scalars['String']['output']>;
  buyFromContactNo?: Maybe<Scalars['String']['output']>;
  buyFromCountryRegionCode?: Maybe<Scalars['String']['output']>;
  buyFromCounty?: Maybe<Scalars['String']['output']>;
  buyFromICPartnerCode?: Maybe<Scalars['String']['output']>;
  buyFromPostCode?: Maybe<Scalars['String']['output']>;
  buyFromVendorName?: Maybe<Scalars['String']['output']>;
  buyFromVendorName2?: Maybe<Scalars['String']['output']>;
  buyFromVendorNo?: Maybe<Scalars['String']['output']>;
  cForm: Scalars['Byte']['output'];
  campaignNo?: Maybe<Scalars['String']['output']>;
  casingFreightTotal: Scalars['Decimal']['output'];
  compressPrepayment: Scalars['Byte']['output'];
  consignmentNoteNo?: Maybe<Scalars['String']['output']>;
  correction: Scalars['Byte']['output'];
  creditorNo?: Maybe<Scalars['String']['output']>;
  currencyCode?: Maybe<Scalars['String']['output']>;
  currencyFactor: Scalars['Decimal']['output'];
  cvd: Scalars['Byte']['output'];
  declarationFormGTA: Scalars['Byte']['output'];
  dimensionSetID: Scalars['Int']['output'];
  docNoOccurrence: Scalars['Int']['output'];
  documentDate?: Maybe<Scalars['DateTime']['output']>;
  documentType: Scalars['Int']['output'];
  dueDate?: Maybe<Scalars['DateTime']['output']>;
  ecomileProcMgr?: Maybe<Scalars['String']['output']>;
  entryPoint?: Maybe<Scalars['String']['output']>;
  exciseBusPostingGroup?: Maybe<Scalars['String']['output']>;
  expectedReceiptDate?: Maybe<Scalars['DateTime']['output']>;
  formCode?: Maybe<Scalars['String']['output']>;
  formNo?: Maybe<Scalars['String']['output']>;
  genBusPostingGroup?: Maybe<Scalars['String']['output']>;
  gstInputServiceDistribution: Scalars['Byte']['output'];
  gstRegistrationNo?: Maybe<Scalars['String']['output']>;
  gstRoundingPrecision: Scalars['Decimal']['output'];
  gstRoundingType: Scalars['Int']['output'];
  gstVendorType: Scalars['Int']['output'];
  icDirection: Scalars['Int']['output'];
  icStatus: Scalars['Int']['output'];
  inboundWhseHandlingTime?: Maybe<Scalars['String']['output']>;
  incomingDocumentEntryNo: Scalars['Int']['output'];
  inputServiceDistribution: Scalars['Byte']['output'];
  inspector?: Maybe<Scalars['String']['output']>;
  invoice: Scalars['Byte']['output'];
  invoiceDiscCode?: Maybe<Scalars['String']['output']>;
  invoiceDiscountCalculation: Scalars['Int']['output'];
  invoiceDiscountValue: Scalars['Decimal']['output'];
  invoiceType: Scalars['Int']['output'];
  jobQueueEntryID: Scalars['UUID']['output'];
  jobQueueStatus: Scalars['Int']['output'];
  languageCode?: Maybe<Scalars['String']['output']>;
  lastPostingNo?: Maybe<Scalars['String']['output']>;
  lastPrepaymentNo?: Maybe<Scalars['String']['output']>;
  lastPrepmtCrMemoNo?: Maybe<Scalars['String']['output']>;
  lastReceivingNo?: Maybe<Scalars['String']['output']>;
  lastReturnShipmentNo?: Maybe<Scalars['String']['output']>;
  lcNo?: Maybe<Scalars['String']['output']>;
  leadTimeCalculation?: Maybe<Scalars['String']['output']>;
  locationCode?: Maybe<Scalars['String']['output']>;
  manufacturerAddress?: Maybe<Scalars['String']['output']>;
  manufacturerECCNo?: Maybe<Scalars['String']['output']>;
  manufacturerName?: Maybe<Scalars['String']['output']>;
  natureOfSupply: Scalars['Int']['output'];
  no?: Maybe<Scalars['String']['output']>;
  noPrinted: Scalars['Int']['output'];
  noSeries?: Maybe<Scalars['String']['output']>;
  onHold?: Maybe<Scalars['String']['output']>;
  orderAddressCode?: Maybe<Scalars['String']['output']>;
  orderClass?: Maybe<Scalars['String']['output']>;
  orderDate?: Maybe<Scalars['DateTime']['output']>;
  orderStatus: Scalars['Int']['output'];
  payToAddress?: Maybe<Scalars['String']['output']>;
  payToAddress2?: Maybe<Scalars['String']['output']>;
  payToCity?: Maybe<Scalars['String']['output']>;
  payToContact?: Maybe<Scalars['String']['output']>;
  payToContactNo?: Maybe<Scalars['String']['output']>;
  payToCountryRegionCode?: Maybe<Scalars['String']['output']>;
  payToCounty?: Maybe<Scalars['String']['output']>;
  payToICPartnerCode?: Maybe<Scalars['String']['output']>;
  payToName?: Maybe<Scalars['String']['output']>;
  payToName2?: Maybe<Scalars['String']['output']>;
  payToPostCode?: Maybe<Scalars['String']['output']>;
  payToVendorNo?: Maybe<Scalars['String']['output']>;
  paymentDiscount: Scalars['Decimal']['output'];
  paymentMethodCode?: Maybe<Scalars['String']['output']>;
  paymentReference?: Maybe<Scalars['String']['output']>;
  paymentTermsCode?: Maybe<Scalars['String']['output']>;
  pmtDiscountDate?: Maybe<Scalars['DateTime']['output']>;
  postingDate?: Maybe<Scalars['DateTime']['output']>;
  postingDescription?: Maybe<Scalars['String']['output']>;
  postingFromWhseRef: Scalars['Int']['output'];
  postingNo?: Maybe<Scalars['String']['output']>;
  postingNoSeries?: Maybe<Scalars['String']['output']>;
  pot: Scalars['Byte']['output'];
  prepayment: Scalars['Decimal']['output'];
  prepaymentDueDate?: Maybe<Scalars['DateTime']['output']>;
  prepaymentNo?: Maybe<Scalars['String']['output']>;
  prepaymentNoSeries?: Maybe<Scalars['String']['output']>;
  prepmtCrMemoNo?: Maybe<Scalars['String']['output']>;
  prepmtCrMemoNoSeries?: Maybe<Scalars['String']['output']>;
  prepmtPaymentDiscount: Scalars['Decimal']['output'];
  prepmtPaymentTermsCode?: Maybe<Scalars['String']['output']>;
  prepmtPmtDiscountDate?: Maybe<Scalars['DateTime']['output']>;
  prepmtPostingDescription?: Maybe<Scalars['String']['output']>;
  pricesIncludingVAT: Scalars['Byte']['output'];
  printPostedDocuments: Scalars['Byte']['output'];
  procCreator?: Maybe<Scalars['String']['output']>;
  promisedReceiptDate?: Maybe<Scalars['DateTime']['output']>;
  purchaserCode?: Maybe<Scalars['String']['output']>;
  quoteNo?: Maybe<Scalars['String']['output']>;
  reasonCode?: Maybe<Scalars['String']['output']>;
  receive: Scalars['Byte']['output'];
  receivingNo?: Maybe<Scalars['String']['output']>;
  receivingNoSeries?: Maybe<Scalars['String']['output']>;
  requestedReceiptDate?: Maybe<Scalars['DateTime']['output']>;
  responsibilityCenter?: Maybe<Scalars['String']['output']>;
  returnShipmentNo?: Maybe<Scalars['String']['output']>;
  returnShipmentNoSeries?: Maybe<Scalars['String']['output']>;
  sellToCustomerNo?: Maybe<Scalars['String']['output']>;
  sendICDocument: Scalars['Byte']['output'];
  serviceTaxRoundingPrecision: Scalars['Decimal']['output'];
  serviceTaxRoundingType: Scalars['Int']['output'];
  serviceTypeRevChrg: Scalars['Int']['output'];
  ship: Scalars['Byte']['output'];
  shipToAddress?: Maybe<Scalars['String']['output']>;
  shipToAddress2?: Maybe<Scalars['String']['output']>;
  shipToCity?: Maybe<Scalars['String']['output']>;
  shipToCode?: Maybe<Scalars['String']['output']>;
  shipToContact?: Maybe<Scalars['String']['output']>;
  shipToCountryRegionCode?: Maybe<Scalars['String']['output']>;
  shipToCounty?: Maybe<Scalars['String']['output']>;
  shipToName?: Maybe<Scalars['String']['output']>;
  shipToName2?: Maybe<Scalars['String']['output']>;
  shipToPostCode?: Maybe<Scalars['String']['output']>;
  shipmentMethodCode?: Maybe<Scalars['String']['output']>;
  shortcutDimension1Code?: Maybe<Scalars['String']['output']>;
  shortcutDimension2Code?: Maybe<Scalars['String']['output']>;
  state?: Maybe<Scalars['String']['output']>;
  status: Scalars['Int']['output'];
  structure?: Maybe<Scalars['String']['output']>;
  subconOrderLineNo: Scalars['Int']['output'];
  subconOrderNo?: Maybe<Scalars['String']['output']>;
  subconpostline: Scalars['Int']['output'];
  subcontracting: Scalars['Byte']['output'];
  taxAreaCode?: Maybe<Scalars['String']['output']>;
  taxLiable: Scalars['Byte']['output'];
  totalInvoiceAmount: Scalars['Decimal']['output'];
  trading: Scalars['Byte']['output'];
  transactionNoServTax: Scalars['Int']['output'];
  transactionSpecification?: Maybe<Scalars['String']['output']>;
  transactionType?: Maybe<Scalars['String']['output']>;
  transitDocument: Scalars['Byte']['output'];
  transportDetails?: Maybe<Scalars['String']['output']>;
  transportMethod?: Maybe<Scalars['String']['output']>;
  vatBaseDiscount: Scalars['Decimal']['output'];
  vatBusPostingGroup?: Maybe<Scalars['String']['output']>;
  vatCountryRegionCode?: Maybe<Scalars['String']['output']>;
  vatRegistrationNo?: Maybe<Scalars['String']['output']>;
  vendorAuthorizationNo?: Maybe<Scalars['String']['output']>;
  vendorCrMemoNo?: Maybe<Scalars['String']['output']>;
  vendorInvoiceDate?: Maybe<Scalars['DateTime']['output']>;
  vendorInvoiceNo?: Maybe<Scalars['String']['output']>;
  vendorOrderNo?: Maybe<Scalars['String']['output']>;
  vendorPostingGroup?: Maybe<Scalars['String']['output']>;
  vendorShipmentDate?: Maybe<Scalars['DateTime']['output']>;
  vendorShipmentNo?: Maybe<Scalars['String']['output']>;
  yourReference?: Maybe<Scalars['String']['output']>;
};

export type PurchaseHeaderFilterInput = {
  and?: InputMaybe<Array<PurchaseHeaderFilterInput>>;
  appliesToDocNo?: InputMaybe<StringOperationFilterInput>;
  appliesToDocType?: InputMaybe<IntOperationFilterInput>;
  appliesToID?: InputMaybe<StringOperationFilterInput>;
  area?: InputMaybe<StringOperationFilterInput>;
  assesseeCode?: InputMaybe<StringOperationFilterInput>;
  assignedUserID?: InputMaybe<StringOperationFilterInput>;
  associatedEnterprises?: InputMaybe<ByteOperationFilterInput>;
  balAccountNo?: InputMaybe<StringOperationFilterInput>;
  balAccountType?: InputMaybe<IntOperationFilterInput>;
  billOfEntryDate?: InputMaybe<DateTimeOperationFilterInput>;
  billOfEntryNo?: InputMaybe<StringOperationFilterInput>;
  billOfEntryValue?: InputMaybe<DecimalOperationFilterInput>;
  buyFromAddress?: InputMaybe<StringOperationFilterInput>;
  buyFromAddress2?: InputMaybe<StringOperationFilterInput>;
  buyFromCity?: InputMaybe<StringOperationFilterInput>;
  buyFromContact?: InputMaybe<StringOperationFilterInput>;
  buyFromContactNo?: InputMaybe<StringOperationFilterInput>;
  buyFromCountryRegionCode?: InputMaybe<StringOperationFilterInput>;
  buyFromCounty?: InputMaybe<StringOperationFilterInput>;
  buyFromICPartnerCode?: InputMaybe<StringOperationFilterInput>;
  buyFromPostCode?: InputMaybe<StringOperationFilterInput>;
  buyFromVendorName?: InputMaybe<StringOperationFilterInput>;
  buyFromVendorName2?: InputMaybe<StringOperationFilterInput>;
  buyFromVendorNo?: InputMaybe<StringOperationFilterInput>;
  cForm?: InputMaybe<ByteOperationFilterInput>;
  campaignNo?: InputMaybe<StringOperationFilterInput>;
  casingFreightTotal?: InputMaybe<DecimalOperationFilterInput>;
  compressPrepayment?: InputMaybe<ByteOperationFilterInput>;
  consignmentNoteNo?: InputMaybe<StringOperationFilterInput>;
  correction?: InputMaybe<ByteOperationFilterInput>;
  creditorNo?: InputMaybe<StringOperationFilterInput>;
  currencyCode?: InputMaybe<StringOperationFilterInput>;
  currencyFactor?: InputMaybe<DecimalOperationFilterInput>;
  cvd?: InputMaybe<ByteOperationFilterInput>;
  declarationFormGTA?: InputMaybe<ByteOperationFilterInput>;
  dimensionSetID?: InputMaybe<IntOperationFilterInput>;
  docNoOccurrence?: InputMaybe<IntOperationFilterInput>;
  documentDate?: InputMaybe<DateTimeOperationFilterInput>;
  documentType?: InputMaybe<IntOperationFilterInput>;
  dueDate?: InputMaybe<DateTimeOperationFilterInput>;
  ecomileProcMgr?: InputMaybe<StringOperationFilterInput>;
  entryPoint?: InputMaybe<StringOperationFilterInput>;
  exciseBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  expectedReceiptDate?: InputMaybe<DateTimeOperationFilterInput>;
  formCode?: InputMaybe<StringOperationFilterInput>;
  formNo?: InputMaybe<StringOperationFilterInput>;
  genBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  gstInputServiceDistribution?: InputMaybe<ByteOperationFilterInput>;
  gstRegistrationNo?: InputMaybe<StringOperationFilterInput>;
  gstRoundingPrecision?: InputMaybe<DecimalOperationFilterInput>;
  gstRoundingType?: InputMaybe<IntOperationFilterInput>;
  gstVendorType?: InputMaybe<IntOperationFilterInput>;
  icDirection?: InputMaybe<IntOperationFilterInput>;
  icStatus?: InputMaybe<IntOperationFilterInput>;
  inboundWhseHandlingTime?: InputMaybe<StringOperationFilterInput>;
  incomingDocumentEntryNo?: InputMaybe<IntOperationFilterInput>;
  inputServiceDistribution?: InputMaybe<ByteOperationFilterInput>;
  inspector?: InputMaybe<StringOperationFilterInput>;
  invoice?: InputMaybe<ByteOperationFilterInput>;
  invoiceDiscCode?: InputMaybe<StringOperationFilterInput>;
  invoiceDiscountCalculation?: InputMaybe<IntOperationFilterInput>;
  invoiceDiscountValue?: InputMaybe<DecimalOperationFilterInput>;
  invoiceType?: InputMaybe<IntOperationFilterInput>;
  jobQueueEntryID?: InputMaybe<UuidOperationFilterInput>;
  jobQueueStatus?: InputMaybe<IntOperationFilterInput>;
  languageCode?: InputMaybe<StringOperationFilterInput>;
  lastPostingNo?: InputMaybe<StringOperationFilterInput>;
  lastPrepaymentNo?: InputMaybe<StringOperationFilterInput>;
  lastPrepmtCrMemoNo?: InputMaybe<StringOperationFilterInput>;
  lastReceivingNo?: InputMaybe<StringOperationFilterInput>;
  lastReturnShipmentNo?: InputMaybe<StringOperationFilterInput>;
  lcNo?: InputMaybe<StringOperationFilterInput>;
  leadTimeCalculation?: InputMaybe<StringOperationFilterInput>;
  locationCode?: InputMaybe<StringOperationFilterInput>;
  manufacturerAddress?: InputMaybe<StringOperationFilterInput>;
  manufacturerECCNo?: InputMaybe<StringOperationFilterInput>;
  manufacturerName?: InputMaybe<StringOperationFilterInput>;
  natureOfSupply?: InputMaybe<IntOperationFilterInput>;
  no?: InputMaybe<StringOperationFilterInput>;
  noPrinted?: InputMaybe<IntOperationFilterInput>;
  noSeries?: InputMaybe<StringOperationFilterInput>;
  onHold?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<PurchaseHeaderFilterInput>>;
  orderAddressCode?: InputMaybe<StringOperationFilterInput>;
  orderClass?: InputMaybe<StringOperationFilterInput>;
  orderDate?: InputMaybe<DateTimeOperationFilterInput>;
  orderStatus?: InputMaybe<IntOperationFilterInput>;
  payToAddress?: InputMaybe<StringOperationFilterInput>;
  payToAddress2?: InputMaybe<StringOperationFilterInput>;
  payToCity?: InputMaybe<StringOperationFilterInput>;
  payToContact?: InputMaybe<StringOperationFilterInput>;
  payToContactNo?: InputMaybe<StringOperationFilterInput>;
  payToCountryRegionCode?: InputMaybe<StringOperationFilterInput>;
  payToCounty?: InputMaybe<StringOperationFilterInput>;
  payToICPartnerCode?: InputMaybe<StringOperationFilterInput>;
  payToName?: InputMaybe<StringOperationFilterInput>;
  payToName2?: InputMaybe<StringOperationFilterInput>;
  payToPostCode?: InputMaybe<StringOperationFilterInput>;
  payToVendorNo?: InputMaybe<StringOperationFilterInput>;
  paymentDiscount?: InputMaybe<DecimalOperationFilterInput>;
  paymentMethodCode?: InputMaybe<StringOperationFilterInput>;
  paymentReference?: InputMaybe<StringOperationFilterInput>;
  paymentTermsCode?: InputMaybe<StringOperationFilterInput>;
  pmtDiscountDate?: InputMaybe<DateTimeOperationFilterInput>;
  postingDate?: InputMaybe<DateTimeOperationFilterInput>;
  postingDescription?: InputMaybe<StringOperationFilterInput>;
  postingFromWhseRef?: InputMaybe<IntOperationFilterInput>;
  postingNo?: InputMaybe<StringOperationFilterInput>;
  postingNoSeries?: InputMaybe<StringOperationFilterInput>;
  pot?: InputMaybe<ByteOperationFilterInput>;
  prepayment?: InputMaybe<DecimalOperationFilterInput>;
  prepaymentDueDate?: InputMaybe<DateTimeOperationFilterInput>;
  prepaymentNo?: InputMaybe<StringOperationFilterInput>;
  prepaymentNoSeries?: InputMaybe<StringOperationFilterInput>;
  prepmtCrMemoNo?: InputMaybe<StringOperationFilterInput>;
  prepmtCrMemoNoSeries?: InputMaybe<StringOperationFilterInput>;
  prepmtPaymentDiscount?: InputMaybe<DecimalOperationFilterInput>;
  prepmtPaymentTermsCode?: InputMaybe<StringOperationFilterInput>;
  prepmtPmtDiscountDate?: InputMaybe<DateTimeOperationFilterInput>;
  prepmtPostingDescription?: InputMaybe<StringOperationFilterInput>;
  pricesIncludingVAT?: InputMaybe<ByteOperationFilterInput>;
  printPostedDocuments?: InputMaybe<ByteOperationFilterInput>;
  procCreator?: InputMaybe<StringOperationFilterInput>;
  promisedReceiptDate?: InputMaybe<DateTimeOperationFilterInput>;
  purchaserCode?: InputMaybe<StringOperationFilterInput>;
  quoteNo?: InputMaybe<StringOperationFilterInput>;
  reasonCode?: InputMaybe<StringOperationFilterInput>;
  receive?: InputMaybe<ByteOperationFilterInput>;
  receivingNo?: InputMaybe<StringOperationFilterInput>;
  receivingNoSeries?: InputMaybe<StringOperationFilterInput>;
  requestedReceiptDate?: InputMaybe<DateTimeOperationFilterInput>;
  responsibilityCenter?: InputMaybe<StringOperationFilterInput>;
  returnShipmentNo?: InputMaybe<StringOperationFilterInput>;
  returnShipmentNoSeries?: InputMaybe<StringOperationFilterInput>;
  sellToCustomerNo?: InputMaybe<StringOperationFilterInput>;
  sendICDocument?: InputMaybe<ByteOperationFilterInput>;
  serviceTaxRoundingPrecision?: InputMaybe<DecimalOperationFilterInput>;
  serviceTaxRoundingType?: InputMaybe<IntOperationFilterInput>;
  serviceTypeRevChrg?: InputMaybe<IntOperationFilterInput>;
  ship?: InputMaybe<ByteOperationFilterInput>;
  shipToAddress?: InputMaybe<StringOperationFilterInput>;
  shipToAddress2?: InputMaybe<StringOperationFilterInput>;
  shipToCity?: InputMaybe<StringOperationFilterInput>;
  shipToCode?: InputMaybe<StringOperationFilterInput>;
  shipToContact?: InputMaybe<StringOperationFilterInput>;
  shipToCountryRegionCode?: InputMaybe<StringOperationFilterInput>;
  shipToCounty?: InputMaybe<StringOperationFilterInput>;
  shipToName?: InputMaybe<StringOperationFilterInput>;
  shipToName2?: InputMaybe<StringOperationFilterInput>;
  shipToPostCode?: InputMaybe<StringOperationFilterInput>;
  shipmentMethodCode?: InputMaybe<StringOperationFilterInput>;
  shortcutDimension1Code?: InputMaybe<StringOperationFilterInput>;
  shortcutDimension2Code?: InputMaybe<StringOperationFilterInput>;
  state?: InputMaybe<StringOperationFilterInput>;
  status?: InputMaybe<IntOperationFilterInput>;
  structure?: InputMaybe<StringOperationFilterInput>;
  subconOrderLineNo?: InputMaybe<IntOperationFilterInput>;
  subconOrderNo?: InputMaybe<StringOperationFilterInput>;
  subconpostline?: InputMaybe<IntOperationFilterInput>;
  subcontracting?: InputMaybe<ByteOperationFilterInput>;
  taxAreaCode?: InputMaybe<StringOperationFilterInput>;
  taxLiable?: InputMaybe<ByteOperationFilterInput>;
  totalInvoiceAmount?: InputMaybe<DecimalOperationFilterInput>;
  trading?: InputMaybe<ByteOperationFilterInput>;
  transactionNoServTax?: InputMaybe<IntOperationFilterInput>;
  transactionSpecification?: InputMaybe<StringOperationFilterInput>;
  transactionType?: InputMaybe<StringOperationFilterInput>;
  transitDocument?: InputMaybe<ByteOperationFilterInput>;
  transportDetails?: InputMaybe<StringOperationFilterInput>;
  transportMethod?: InputMaybe<StringOperationFilterInput>;
  vatBaseDiscount?: InputMaybe<DecimalOperationFilterInput>;
  vatBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  vatCountryRegionCode?: InputMaybe<StringOperationFilterInput>;
  vatRegistrationNo?: InputMaybe<StringOperationFilterInput>;
  vendorAuthorizationNo?: InputMaybe<StringOperationFilterInput>;
  vendorCrMemoNo?: InputMaybe<StringOperationFilterInput>;
  vendorInvoiceDate?: InputMaybe<DateTimeOperationFilterInput>;
  vendorInvoiceNo?: InputMaybe<StringOperationFilterInput>;
  vendorOrderNo?: InputMaybe<StringOperationFilterInput>;
  vendorPostingGroup?: InputMaybe<StringOperationFilterInput>;
  vendorShipmentDate?: InputMaybe<DateTimeOperationFilterInput>;
  vendorShipmentNo?: InputMaybe<StringOperationFilterInput>;
  yourReference?: InputMaybe<StringOperationFilterInput>;
};

export type PurchaseHeaderSortInput = {
  appliesToDocNo?: InputMaybe<SortEnumType>;
  appliesToDocType?: InputMaybe<SortEnumType>;
  appliesToID?: InputMaybe<SortEnumType>;
  area?: InputMaybe<SortEnumType>;
  assesseeCode?: InputMaybe<SortEnumType>;
  assignedUserID?: InputMaybe<SortEnumType>;
  associatedEnterprises?: InputMaybe<SortEnumType>;
  balAccountNo?: InputMaybe<SortEnumType>;
  balAccountType?: InputMaybe<SortEnumType>;
  billOfEntryDate?: InputMaybe<SortEnumType>;
  billOfEntryNo?: InputMaybe<SortEnumType>;
  billOfEntryValue?: InputMaybe<SortEnumType>;
  buyFromAddress?: InputMaybe<SortEnumType>;
  buyFromAddress2?: InputMaybe<SortEnumType>;
  buyFromCity?: InputMaybe<SortEnumType>;
  buyFromContact?: InputMaybe<SortEnumType>;
  buyFromContactNo?: InputMaybe<SortEnumType>;
  buyFromCountryRegionCode?: InputMaybe<SortEnumType>;
  buyFromCounty?: InputMaybe<SortEnumType>;
  buyFromICPartnerCode?: InputMaybe<SortEnumType>;
  buyFromPostCode?: InputMaybe<SortEnumType>;
  buyFromVendorName?: InputMaybe<SortEnumType>;
  buyFromVendorName2?: InputMaybe<SortEnumType>;
  buyFromVendorNo?: InputMaybe<SortEnumType>;
  cForm?: InputMaybe<SortEnumType>;
  campaignNo?: InputMaybe<SortEnumType>;
  casingFreightTotal?: InputMaybe<SortEnumType>;
  compressPrepayment?: InputMaybe<SortEnumType>;
  consignmentNoteNo?: InputMaybe<SortEnumType>;
  correction?: InputMaybe<SortEnumType>;
  creditorNo?: InputMaybe<SortEnumType>;
  currencyCode?: InputMaybe<SortEnumType>;
  currencyFactor?: InputMaybe<SortEnumType>;
  cvd?: InputMaybe<SortEnumType>;
  declarationFormGTA?: InputMaybe<SortEnumType>;
  dimensionSetID?: InputMaybe<SortEnumType>;
  docNoOccurrence?: InputMaybe<SortEnumType>;
  documentDate?: InputMaybe<SortEnumType>;
  documentType?: InputMaybe<SortEnumType>;
  dueDate?: InputMaybe<SortEnumType>;
  ecomileProcMgr?: InputMaybe<SortEnumType>;
  entryPoint?: InputMaybe<SortEnumType>;
  exciseBusPostingGroup?: InputMaybe<SortEnumType>;
  expectedReceiptDate?: InputMaybe<SortEnumType>;
  formCode?: InputMaybe<SortEnumType>;
  formNo?: InputMaybe<SortEnumType>;
  genBusPostingGroup?: InputMaybe<SortEnumType>;
  gstInputServiceDistribution?: InputMaybe<SortEnumType>;
  gstRegistrationNo?: InputMaybe<SortEnumType>;
  gstRoundingPrecision?: InputMaybe<SortEnumType>;
  gstRoundingType?: InputMaybe<SortEnumType>;
  gstVendorType?: InputMaybe<SortEnumType>;
  icDirection?: InputMaybe<SortEnumType>;
  icStatus?: InputMaybe<SortEnumType>;
  inboundWhseHandlingTime?: InputMaybe<SortEnumType>;
  incomingDocumentEntryNo?: InputMaybe<SortEnumType>;
  inputServiceDistribution?: InputMaybe<SortEnumType>;
  inspector?: InputMaybe<SortEnumType>;
  invoice?: InputMaybe<SortEnumType>;
  invoiceDiscCode?: InputMaybe<SortEnumType>;
  invoiceDiscountCalculation?: InputMaybe<SortEnumType>;
  invoiceDiscountValue?: InputMaybe<SortEnumType>;
  invoiceType?: InputMaybe<SortEnumType>;
  jobQueueEntryID?: InputMaybe<SortEnumType>;
  jobQueueStatus?: InputMaybe<SortEnumType>;
  languageCode?: InputMaybe<SortEnumType>;
  lastPostingNo?: InputMaybe<SortEnumType>;
  lastPrepaymentNo?: InputMaybe<SortEnumType>;
  lastPrepmtCrMemoNo?: InputMaybe<SortEnumType>;
  lastReceivingNo?: InputMaybe<SortEnumType>;
  lastReturnShipmentNo?: InputMaybe<SortEnumType>;
  lcNo?: InputMaybe<SortEnumType>;
  leadTimeCalculation?: InputMaybe<SortEnumType>;
  locationCode?: InputMaybe<SortEnumType>;
  manufacturerAddress?: InputMaybe<SortEnumType>;
  manufacturerECCNo?: InputMaybe<SortEnumType>;
  manufacturerName?: InputMaybe<SortEnumType>;
  natureOfSupply?: InputMaybe<SortEnumType>;
  no?: InputMaybe<SortEnumType>;
  noPrinted?: InputMaybe<SortEnumType>;
  noSeries?: InputMaybe<SortEnumType>;
  onHold?: InputMaybe<SortEnumType>;
  orderAddressCode?: InputMaybe<SortEnumType>;
  orderClass?: InputMaybe<SortEnumType>;
  orderDate?: InputMaybe<SortEnumType>;
  orderStatus?: InputMaybe<SortEnumType>;
  payToAddress?: InputMaybe<SortEnumType>;
  payToAddress2?: InputMaybe<SortEnumType>;
  payToCity?: InputMaybe<SortEnumType>;
  payToContact?: InputMaybe<SortEnumType>;
  payToContactNo?: InputMaybe<SortEnumType>;
  payToCountryRegionCode?: InputMaybe<SortEnumType>;
  payToCounty?: InputMaybe<SortEnumType>;
  payToICPartnerCode?: InputMaybe<SortEnumType>;
  payToName?: InputMaybe<SortEnumType>;
  payToName2?: InputMaybe<SortEnumType>;
  payToPostCode?: InputMaybe<SortEnumType>;
  payToVendorNo?: InputMaybe<SortEnumType>;
  paymentDiscount?: InputMaybe<SortEnumType>;
  paymentMethodCode?: InputMaybe<SortEnumType>;
  paymentReference?: InputMaybe<SortEnumType>;
  paymentTermsCode?: InputMaybe<SortEnumType>;
  pmtDiscountDate?: InputMaybe<SortEnumType>;
  postingDate?: InputMaybe<SortEnumType>;
  postingDescription?: InputMaybe<SortEnumType>;
  postingFromWhseRef?: InputMaybe<SortEnumType>;
  postingNo?: InputMaybe<SortEnumType>;
  postingNoSeries?: InputMaybe<SortEnumType>;
  pot?: InputMaybe<SortEnumType>;
  prepayment?: InputMaybe<SortEnumType>;
  prepaymentDueDate?: InputMaybe<SortEnumType>;
  prepaymentNo?: InputMaybe<SortEnumType>;
  prepaymentNoSeries?: InputMaybe<SortEnumType>;
  prepmtCrMemoNo?: InputMaybe<SortEnumType>;
  prepmtCrMemoNoSeries?: InputMaybe<SortEnumType>;
  prepmtPaymentDiscount?: InputMaybe<SortEnumType>;
  prepmtPaymentTermsCode?: InputMaybe<SortEnumType>;
  prepmtPmtDiscountDate?: InputMaybe<SortEnumType>;
  prepmtPostingDescription?: InputMaybe<SortEnumType>;
  pricesIncludingVAT?: InputMaybe<SortEnumType>;
  printPostedDocuments?: InputMaybe<SortEnumType>;
  procCreator?: InputMaybe<SortEnumType>;
  promisedReceiptDate?: InputMaybe<SortEnumType>;
  purchaserCode?: InputMaybe<SortEnumType>;
  quoteNo?: InputMaybe<SortEnumType>;
  reasonCode?: InputMaybe<SortEnumType>;
  receive?: InputMaybe<SortEnumType>;
  receivingNo?: InputMaybe<SortEnumType>;
  receivingNoSeries?: InputMaybe<SortEnumType>;
  requestedReceiptDate?: InputMaybe<SortEnumType>;
  responsibilityCenter?: InputMaybe<SortEnumType>;
  returnShipmentNo?: InputMaybe<SortEnumType>;
  returnShipmentNoSeries?: InputMaybe<SortEnumType>;
  sellToCustomerNo?: InputMaybe<SortEnumType>;
  sendICDocument?: InputMaybe<SortEnumType>;
  serviceTaxRoundingPrecision?: InputMaybe<SortEnumType>;
  serviceTaxRoundingType?: InputMaybe<SortEnumType>;
  serviceTypeRevChrg?: InputMaybe<SortEnumType>;
  ship?: InputMaybe<SortEnumType>;
  shipToAddress?: InputMaybe<SortEnumType>;
  shipToAddress2?: InputMaybe<SortEnumType>;
  shipToCity?: InputMaybe<SortEnumType>;
  shipToCode?: InputMaybe<SortEnumType>;
  shipToContact?: InputMaybe<SortEnumType>;
  shipToCountryRegionCode?: InputMaybe<SortEnumType>;
  shipToCounty?: InputMaybe<SortEnumType>;
  shipToName?: InputMaybe<SortEnumType>;
  shipToName2?: InputMaybe<SortEnumType>;
  shipToPostCode?: InputMaybe<SortEnumType>;
  shipmentMethodCode?: InputMaybe<SortEnumType>;
  shortcutDimension1Code?: InputMaybe<SortEnumType>;
  shortcutDimension2Code?: InputMaybe<SortEnumType>;
  state?: InputMaybe<SortEnumType>;
  status?: InputMaybe<SortEnumType>;
  structure?: InputMaybe<SortEnumType>;
  subconOrderLineNo?: InputMaybe<SortEnumType>;
  subconOrderNo?: InputMaybe<SortEnumType>;
  subconpostline?: InputMaybe<SortEnumType>;
  subcontracting?: InputMaybe<SortEnumType>;
  taxAreaCode?: InputMaybe<SortEnumType>;
  taxLiable?: InputMaybe<SortEnumType>;
  totalInvoiceAmount?: InputMaybe<SortEnumType>;
  trading?: InputMaybe<SortEnumType>;
  transactionNoServTax?: InputMaybe<SortEnumType>;
  transactionSpecification?: InputMaybe<SortEnumType>;
  transactionType?: InputMaybe<SortEnumType>;
  transitDocument?: InputMaybe<SortEnumType>;
  transportDetails?: InputMaybe<SortEnumType>;
  transportMethod?: InputMaybe<SortEnumType>;
  vatBaseDiscount?: InputMaybe<SortEnumType>;
  vatBusPostingGroup?: InputMaybe<SortEnumType>;
  vatCountryRegionCode?: InputMaybe<SortEnumType>;
  vatRegistrationNo?: InputMaybe<SortEnumType>;
  vendorAuthorizationNo?: InputMaybe<SortEnumType>;
  vendorCrMemoNo?: InputMaybe<SortEnumType>;
  vendorInvoiceDate?: InputMaybe<SortEnumType>;
  vendorInvoiceNo?: InputMaybe<SortEnumType>;
  vendorOrderNo?: InputMaybe<SortEnumType>;
  vendorPostingGroup?: InputMaybe<SortEnumType>;
  vendorShipmentDate?: InputMaybe<SortEnumType>;
  vendorShipmentNo?: InputMaybe<SortEnumType>;
  yourReference?: InputMaybe<SortEnumType>;
};

/** A connection to a list of items. */
export type PurchaseItemNosConnection = {
  __typename?: 'PurchaseItemNosConnection';
  /** A list of edges. */
  edges?: Maybe<Array<PurchaseItemNosEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<CasingItem>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type PurchaseItemNosEdge = {
  __typename?: 'PurchaseItemNosEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: CasingItem;
};

export type PurchaseLine = {
  __typename?: 'PurchaseLine';
  aRcdNotInvExVATLCY: Scalars['Decimal']['output'];
  adcvatAmount: Scalars['Decimal']['output'];
  adeAmount: Scalars['Decimal']['output'];
  adetAmount: Scalars['Decimal']['output'];
  aedgsiAmount: Scalars['Decimal']['output'];
  aedttaAmount: Scalars['Decimal']['output'];
  allowInvoiceDisc: Scalars['Byte']['output'];
  allowItemChargeAssignment: Scalars['Byte']['output'];
  amount: Scalars['Decimal']['output'];
  amountAddedToExciseBase: Scalars['Decimal']['output'];
  amountAddedToInventory: Scalars['Decimal']['output'];
  amountAddedToTaxBase: Scalars['Decimal']['output'];
  amountIncludingExcise: Scalars['Decimal']['output'];
  amountIncludingTax: Scalars['Decimal']['output'];
  amountIncludingVAT: Scalars['Decimal']['output'];
  amountLoadedOnInventory: Scalars['Decimal']['output'];
  amountToVendor: Scalars['Decimal']['output'];
  amtInclServiceTaxIntm: Scalars['Decimal']['output'];
  amtRcdNotInvoiced: Scalars['Decimal']['output'];
  amtRcdNotInvoicedLCY: Scalars['Decimal']['output'];
  applToItemEntry: Scalars['Int']['output'];
  appliesToIDDelivery?: Maybe<Scalars['String']['output']>;
  appliesToIDReceipt?: Maybe<Scalars['String']['output']>;
  area?: Maybe<Scalars['String']['output']>;
  assessableValue: Scalars['Decimal']['output'];
  assesseeCode?: Maybe<Scalars['String']['output']>;
  attachedToLineNo: Scalars['Int']['output'];
  balTDSIncludingSHECESS: Scalars['Decimal']['output'];
  bcdAmount: Scalars['Decimal']['output'];
  bedAmount: Scalars['Decimal']['output'];
  binCode?: Maybe<Scalars['String']['output']>;
  blanketOrderLineNo: Scalars['Int']['output'];
  blanketOrderNo?: Maybe<Scalars['String']['output']>;
  budgetedFANo?: Maybe<Scalars['String']['output']>;
  button?: Maybe<Scalars['String']['output']>;
  buyFromVendorNo?: Maybe<Scalars['String']['output']>;
  capitalItem: Scalars['Byte']['output'];
  casingCondition: Scalars['Int']['output'];
  casingFreight: Scalars['Decimal']['output'];
  cessAmount: Scalars['Decimal']['output'];
  chargesToVendor: Scalars['Decimal']['output'];
  cifAmount: Scalars['Decimal']['output'];
  completelyReceived: Scalars['Byte']['output'];
  componentItemNo?: Maybe<Scalars['String']['output']>;
  concessionalCode?: Maybe<Scalars['String']['output']>;
  crossReferenceNo?: Maybe<Scalars['String']['output']>;
  crossReferenceType: Scalars['Int']['output'];
  crossReferenceTypeNo?: Maybe<Scalars['String']['output']>;
  ctshNo?: Maybe<Scalars['String']['output']>;
  currencyCode?: Maybe<Scalars['String']['output']>;
  customDutyAmount: Scalars['Decimal']['output'];
  customEcessAmount: Scalars['Decimal']['output'];
  customShecessAmount: Scalars['Decimal']['output'];
  cvd: Scalars['Byte']['output'];
  cwipglType: Scalars['Int']['output'];
  deliverCompFor: Scalars['Decimal']['output'];
  deliveryChallanDate?: Maybe<Scalars['DateTime']['output']>;
  deprAcquisitionCost: Scalars['Byte']['output'];
  deprUntilFAPostingDate: Scalars['Byte']['output'];
  depreciationBookCode?: Maybe<Scalars['String']['output']>;
  description?: Maybe<Scalars['String']['output']>;
  description2?: Maybe<Scalars['String']['output']>;
  dimensionSetID: Scalars['Int']['output'];
  directUnitCost: Scalars['Decimal']['output'];
  dispatchDate?: Maybe<Scalars['DateTime']['output']>;
  dispatchDestination?: Maybe<Scalars['String']['output']>;
  dispatchMobileNo?: Maybe<Scalars['String']['output']>;
  dispatchOrderNo?: Maybe<Scalars['String']['output']>;
  dispatchTransporter?: Maybe<Scalars['String']['output']>;
  dispatchVehicleNo?: Maybe<Scalars['String']['output']>;
  documentNo?: Maybe<Scalars['String']['output']>;
  documentType: Scalars['Int']['output'];
  dropShipment: Scalars['Byte']['output'];
  duplicateInDepreciationBook?: Maybe<Scalars['String']['output']>;
  ecessAmount: Scalars['Decimal']['output'];
  ecessOnTDS: Scalars['Decimal']['output'];
  ecessOnTDSAmount: Scalars['Decimal']['output'];
  entryPoint?: Maybe<Scalars['String']['output']>;
  exciseAccountingType: Scalars['Int']['output'];
  exciseAmount: Scalars['Decimal']['output'];
  exciseBaseAmount: Scalars['Decimal']['output'];
  exciseBaseQuantity: Scalars['Decimal']['output'];
  exciseBusPostingGroup?: Maybe<Scalars['String']['output']>;
  exciseCreditReversal: Scalars['Byte']['output'];
  exciseLoadingOnInventory: Scalars['Byte']['output'];
  exciseProdPostingGroup?: Maybe<Scalars['String']['output']>;
  exciseRefund: Scalars['Byte']['output'];
  exempted: Scalars['Byte']['output'];
  expectedReceiptDate?: Maybe<Scalars['DateTime']['output']>;
  faNo?: Maybe<Scalars['String']['output']>;
  faPostingDate?: Maybe<Scalars['DateTime']['output']>;
  faPostingType: Scalars['Int']['output'];
  factCasingCondition: Scalars['Int']['output'];
  factInspectionDate?: Maybe<Scalars['DateTime']['output']>;
  factInspector?: Maybe<Scalars['String']['output']>;
  factInspectorFinal?: Maybe<Scalars['String']['output']>;
  finished: Scalars['Byte']['output'];
  formCode?: Maybe<Scalars['String']['output']>;
  formNo?: Maybe<Scalars['String']['output']>;
  genBusPostingGroup?: Maybe<Scalars['String']['output']>;
  genProdPostingGroup?: Maybe<Scalars['String']['output']>;
  grossWeight: Scalars['Decimal']['output'];
  gst: Scalars['Decimal']['output'];
  gstBaseAmount: Scalars['Decimal']['output'];
  gstCredit: Scalars['Int']['output'];
  gstGroupCode?: Maybe<Scalars['String']['output']>;
  gstGroupType: Scalars['Int']['output'];
  gstJurisdictionType: Scalars['Int']['output'];
  hsnsacCode?: Maybe<Scalars['String']['output']>;
  icPartnerCode?: Maybe<Scalars['String']['output']>;
  icPartnerRefType: Scalars['Int']['output'];
  icPartnerReference?: Maybe<Scalars['String']['output']>;
  inboundWhseHandlingTime?: Maybe<Scalars['String']['output']>;
  indirectCost: Scalars['Decimal']['output'];
  inputTaxCreditAmount: Scalars['Decimal']['output'];
  inspection?: Maybe<Scalars['String']['output']>;
  inspector?: Maybe<Scalars['String']['output']>;
  insuranceNo?: Maybe<Scalars['String']['output']>;
  invDiscAmountToInvoice: Scalars['Decimal']['output'];
  invDiscountAmount: Scalars['Decimal']['output'];
  itemCategoryCode?: Maybe<Scalars['String']['output']>;
  itemChargeEntry: Scalars['Byte']['output'];
  jobCurrencyCode?: Maybe<Scalars['String']['output']>;
  jobCurrencyFactor: Scalars['Decimal']['output'];
  jobLineAmount: Scalars['Decimal']['output'];
  jobLineAmountLCY: Scalars['Decimal']['output'];
  jobLineDiscAmountLCY: Scalars['Decimal']['output'];
  jobLineDiscount: Scalars['Decimal']['output'];
  jobLineDiscountAmount: Scalars['Decimal']['output'];
  jobLineType: Scalars['Int']['output'];
  jobNo?: Maybe<Scalars['String']['output']>;
  jobPlanningLineNo: Scalars['Int']['output'];
  jobRemainingQty: Scalars['Decimal']['output'];
  jobRemainingQtyBase: Scalars['Decimal']['output'];
  jobTaskNo?: Maybe<Scalars['String']['output']>;
  jobTotalPrice: Scalars['Decimal']['output'];
  jobTotalPriceLCY: Scalars['Decimal']['output'];
  jobUnitPrice: Scalars['Decimal']['output'];
  jobUnitPriceLCY: Scalars['Decimal']['output'];
  leadTimeCalculation?: Maybe<Scalars['String']['output']>;
  lineAmount: Scalars['Decimal']['output'];
  lineDiscount: Scalars['Decimal']['output'];
  lineDiscountAmount: Scalars['Decimal']['output'];
  lineNo: Scalars['Int']['output'];
  locationCode?: Maybe<Scalars['String']['output']>;
  lug: Scalars['Int']['output'];
  maintenanceCode?: Maybe<Scalars['String']['output']>;
  make?: Maybe<Scalars['String']['output']>;
  model?: Maybe<Scalars['String']['output']>;
  mpsOrder: Scalars['Byte']['output'];
  nccdAmount: Scalars['Decimal']['output'];
  netWeight: Scalars['Decimal']['output'];
  newSerialNo?: Maybe<Scalars['String']['output']>;
  no?: Maybe<Scalars['String']['output']>;
  noOfCuts: Scalars['Int']['output'];
  noOfRolls: Scalars['Int']['output'];
  noOfStrips: Scalars['Int']['output'];
  nonITCClaimableUsage: Scalars['Decimal']['output'];
  nonstock: Scalars['Byte']['output'];
  notificationNo?: Maybe<Scalars['String']['output']>;
  notificationSlNo?: Maybe<Scalars['String']['output']>;
  operationNo?: Maybe<Scalars['String']['output']>;
  orderDate?: Maybe<Scalars['DateTime']['output']>;
  orderStatus: Scalars['Int']['output'];
  outstandingAmount: Scalars['Decimal']['output'];
  outstandingAmountLCY: Scalars['Decimal']['output'];
  outstandingAmtExVATLCY: Scalars['Decimal']['output'];
  outstandingQtyBase: Scalars['Decimal']['output'];
  outstandingQuantity: Scalars['Decimal']['output'];
  overheadRate: Scalars['Decimal']['output'];
  pattern?: Maybe<Scalars['String']['output']>;
  payToVendorNo?: Maybe<Scalars['String']['output']>;
  perContract: Scalars['Byte']['output'];
  plannedReceiptDate?: Maybe<Scalars['DateTime']['output']>;
  planningFlexibility: Scalars['Int']['output'];
  ply: Scalars['Int']['output'];
  postingDate?: Maybe<Scalars['DateTime']['output']>;
  postingGroup?: Maybe<Scalars['String']['output']>;
  prepayment: Scalars['Decimal']['output'];
  prepaymentAmount: Scalars['Decimal']['output'];
  prepaymentLine: Scalars['Byte']['output'];
  prepaymentTaxAreaCode?: Maybe<Scalars['String']['output']>;
  prepaymentTaxGroupCode?: Maybe<Scalars['String']['output']>;
  prepaymentTaxLiable: Scalars['Byte']['output'];
  prepaymentVAT: Scalars['Decimal']['output'];
  prepaymentVATDifference: Scalars['Decimal']['output'];
  prepaymentVATIdentifier?: Maybe<Scalars['String']['output']>;
  prepmtAmountInvInclVAT: Scalars['Decimal']['output'];
  prepmtAmountInvLCY: Scalars['Decimal']['output'];
  prepmtAmtDeducted: Scalars['Decimal']['output'];
  prepmtAmtInclVAT: Scalars['Decimal']['output'];
  prepmtAmtInv: Scalars['Decimal']['output'];
  prepmtAmtToDeduct: Scalars['Decimal']['output'];
  prepmtLineAmount: Scalars['Decimal']['output'];
  prepmtVATAmountInvLCY: Scalars['Decimal']['output'];
  prepmtVATBaseAmt: Scalars['Decimal']['output'];
  prepmtVATCalcType: Scalars['Int']['output'];
  prepmtVATDiffDeducted: Scalars['Decimal']['output'];
  prepmtVATDiffToDeduct: Scalars['Decimal']['output'];
  procOrderNo?: Maybe<Scalars['String']['output']>;
  prodOrderLineNo: Scalars['Int']['output'];
  prodOrderNo?: Maybe<Scalars['String']['output']>;
  productGroupCode?: Maybe<Scalars['String']['output']>;
  profit: Scalars['Decimal']['output'];
  promisedReceiptDate?: Maybe<Scalars['DateTime']['output']>;
  purOrderNo?: Maybe<Scalars['String']['output']>;
  purchasingCode?: Maybe<Scalars['String']['output']>;
  qtyInvoicedBase: Scalars['Decimal']['output'];
  qtyPerUnitOfMeasure: Scalars['Decimal']['output'];
  qtyRcdNotInvoiced: Scalars['Decimal']['output'];
  qtyRcdNotInvoicedBase: Scalars['Decimal']['output'];
  qtyReceivedBase: Scalars['Decimal']['output'];
  qtyRejectedCE: Scalars['Decimal']['output'];
  qtyRejectedRework: Scalars['Decimal']['output'];
  qtyRejectedVE: Scalars['Decimal']['output'];
  qtyToInvoice: Scalars['Decimal']['output'];
  qtyToInvoiceBase: Scalars['Decimal']['output'];
  qtyToReceive: Scalars['Decimal']['output'];
  qtyToReceiveBase: Scalars['Decimal']['output'];
  qtyToRejectCE: Scalars['Decimal']['output'];
  qtyToRejectRework: Scalars['Decimal']['output'];
  qtyToRejectVE: Scalars['Decimal']['output'];
  quantity: Scalars['Decimal']['output'];
  quantityBase: Scalars['Decimal']['output'];
  quantityInvoiced: Scalars['Decimal']['output'];
  quantityReceived: Scalars['Decimal']['output'];
  reasonCode?: Maybe<Scalars['String']['output']>;
  receiptDate?: Maybe<Scalars['DateTime']['output']>;
  receiptLineNo: Scalars['Int']['output'];
  receiptNo?: Maybe<Scalars['String']['output']>;
  receiverID?: Maybe<Scalars['String']['output']>;
  rejectionReason?: Maybe<Scalars['String']['output']>;
  releasedProductionOrder?: Maybe<Scalars['String']['output']>;
  requestedReceiptDate?: Maybe<Scalars['DateTime']['output']>;
  responsibilityCenter?: Maybe<Scalars['String']['output']>;
  retQtyShpdNotInvdBase: Scalars['Decimal']['output'];
  returnQtyShipped: Scalars['Decimal']['output'];
  returnQtyShippedBase: Scalars['Decimal']['output'];
  returnQtyShippedNotInvd: Scalars['Decimal']['output'];
  returnQtyToShip: Scalars['Decimal']['output'];
  returnQtyToShipBase: Scalars['Decimal']['output'];
  returnReasonCode?: Maybe<Scalars['String']['output']>;
  returnShipmentLineNo: Scalars['Int']['output'];
  returnShipmentNo?: Maybe<Scalars['String']['output']>;
  returnShpdNotInvd: Scalars['Decimal']['output'];
  returnShpdNotInvdLCY: Scalars['Decimal']['output'];
  routingNo?: Maybe<Scalars['String']['output']>;
  routingReferenceNo: Scalars['Int']['output'];
  sTaxAmountIntm: Scalars['Decimal']['output'];
  sTaxBaseAmountIntm: Scalars['Decimal']['output'];
  sTaxEcessAmountIntm: Scalars['Decimal']['output'];
  sTaxSHECessAmountIntm: Scalars['Decimal']['output'];
  saedAmount: Scalars['Decimal']['output'];
  safetyLeadTime?: Maybe<Scalars['String']['output']>;
  salesOrderLineNo: Scalars['Int']['output'];
  salesOrderNo?: Maybe<Scalars['String']['output']>;
  salvageValue: Scalars['Decimal']['output'];
  scrapTyre: Scalars['Byte']['output'];
  sedAmount: Scalars['Decimal']['output'];
  segmentCode?: Maybe<Scalars['String']['output']>;
  sendforrework: Scalars['Byte']['output'];
  serialNo?: Maybe<Scalars['String']['output']>;
  serviceTaxAmount: Scalars['Decimal']['output'];
  serviceTaxBase: Scalars['Decimal']['output'];
  serviceTaxEcessAmount: Scalars['Decimal']['output'];
  serviceTaxGroup?: Maybe<Scalars['String']['output']>;
  serviceTaxRegistrationNo?: Maybe<Scalars['String']['output']>;
  serviceTaxSHECessAmount: Scalars['Decimal']['output'];
  setoffAvailable: Scalars['Byte']['output'];
  sheCessAmount: Scalars['Decimal']['output'];
  sheCessOnTDS: Scalars['Decimal']['output'];
  sheCessOnTDSAmount: Scalars['Decimal']['output'];
  shortcutDimension1Code?: Maybe<Scalars['String']['output']>;
  shortcutDimension2Code?: Maybe<Scalars['String']['output']>;
  skipNewNumber: Scalars['Byte']['output'];
  sourceDocumentNo?: Maybe<Scalars['String']['output']>;
  sourceDocumentType: Scalars['Int']['output'];
  specialOrder: Scalars['Byte']['output'];
  specialOrderSalesLineNo: Scalars['Int']['output'];
  specialOrderSalesNo?: Maybe<Scalars['String']['output']>;
  stateCode?: Maybe<Scalars['String']['output']>;
  status: Scalars['Int']['output'];
  subMake?: Maybe<Scalars['String']['output']>;
  subconreceive: Scalars['Byte']['output'];
  subconsend: Scalars['Byte']['output'];
  subcontracting: Scalars['Byte']['output'];
  supplementary: Scalars['Byte']['output'];
  surcharge: Scalars['Decimal']['output'];
  surchargeAmount: Scalars['Decimal']['output'];
  surchargeBaseAmount: Scalars['Decimal']['output'];
  systemCreatedEntry: Scalars['Byte']['output'];
  tax: Scalars['Decimal']['output'];
  taxAmount: Scalars['Decimal']['output'];
  taxAreaCode?: Maybe<Scalars['String']['output']>;
  taxBaseAmount: Scalars['Decimal']['output'];
  taxGroupCode?: Maybe<Scalars['String']['output']>;
  taxLiable: Scalars['Byte']['output'];
  tcsOnPurchase: Scalars['Decimal']['output'];
  tcsOnPurchase2: Scalars['Decimal']['output'];
  tds: Scalars['Decimal']['output'];
  tdsAmount: Scalars['Decimal']['output'];
  tdsAmountIncludingSurcharge: Scalars['Decimal']['output'];
  tdsBaseAmount: Scalars['Decimal']['output'];
  tdsCategory: Scalars['Int']['output'];
  tdsGroup: Scalars['Int']['output'];
  tdsNatureOfDeduction?: Maybe<Scalars['String']['output']>;
  tempTDSBase: Scalars['Decimal']['output'];
  totServTaxAmountIntm: Scalars['Decimal']['output'];
  totalGSTAmount: Scalars['Decimal']['output'];
  totalTDSIncludingSHECESS: Scalars['Decimal']['output'];
  transactionSpecification?: Maybe<Scalars['String']['output']>;
  transactionType?: Maybe<Scalars['String']['output']>;
  transportMethod?: Maybe<Scalars['String']['output']>;
  type: Scalars['Int']['output'];
  unitCost: Scalars['Decimal']['output'];
  unitCostLCY: Scalars['Decimal']['output'];
  unitOfMeasure?: Maybe<Scalars['String']['output']>;
  unitOfMeasureCode?: Maybe<Scalars['String']['output']>;
  unitOfMeasureCrossRef?: Maybe<Scalars['String']['output']>;
  unitPriceLCY: Scalars['Decimal']['output'];
  unitVolume: Scalars['Decimal']['output'];
  unitsPerParcel: Scalars['Decimal']['output'];
  useDuplicationList: Scalars['Byte']['output'];
  useTax: Scalars['Byte']['output'];
  variantCode?: Maybe<Scalars['String']['output']>;
  vat: Scalars['Decimal']['output'];
  vatAblePurchaseTaxAmount: Scalars['Decimal']['output'];
  vatBaseAmount: Scalars['Decimal']['output'];
  vatBusPostingGroup?: Maybe<Scalars['String']['output']>;
  vatCalculationType: Scalars['Int']['output'];
  vatDifference: Scalars['Decimal']['output'];
  vatIdentifier?: Maybe<Scalars['String']['output']>;
  vatProdPostingGroup?: Maybe<Scalars['String']['output']>;
  vatType: Scalars['Int']['output'];
  vendorItemNo?: Maybe<Scalars['String']['output']>;
  vendorShipmentNo?: Maybe<Scalars['String']['output']>;
  weightPerRoll: Scalars['Decimal']['output'];
  weightPerStrip: Scalars['Decimal']['output'];
  workCenterNo?: Maybe<Scalars['String']['output']>;
  workTax: Scalars['Decimal']['output'];
  workTaxAmount: Scalars['Decimal']['output'];
  workTaxBaseAmount: Scalars['Decimal']['output'];
  workTaxGroup: Scalars['Int']['output'];
  workTaxNatureOfDeduction?: Maybe<Scalars['String']['output']>;
};

export type PurchaseLineFilterInput = {
  aRcdNotInvExVATLCY?: InputMaybe<DecimalOperationFilterInput>;
  adcvatAmount?: InputMaybe<DecimalOperationFilterInput>;
  adeAmount?: InputMaybe<DecimalOperationFilterInput>;
  adetAmount?: InputMaybe<DecimalOperationFilterInput>;
  aedgsiAmount?: InputMaybe<DecimalOperationFilterInput>;
  aedttaAmount?: InputMaybe<DecimalOperationFilterInput>;
  allowInvoiceDisc?: InputMaybe<ByteOperationFilterInput>;
  allowItemChargeAssignment?: InputMaybe<ByteOperationFilterInput>;
  amount?: InputMaybe<DecimalOperationFilterInput>;
  amountAddedToExciseBase?: InputMaybe<DecimalOperationFilterInput>;
  amountAddedToInventory?: InputMaybe<DecimalOperationFilterInput>;
  amountAddedToTaxBase?: InputMaybe<DecimalOperationFilterInput>;
  amountIncludingExcise?: InputMaybe<DecimalOperationFilterInput>;
  amountIncludingTax?: InputMaybe<DecimalOperationFilterInput>;
  amountIncludingVAT?: InputMaybe<DecimalOperationFilterInput>;
  amountLoadedOnInventory?: InputMaybe<DecimalOperationFilterInput>;
  amountToVendor?: InputMaybe<DecimalOperationFilterInput>;
  amtInclServiceTaxIntm?: InputMaybe<DecimalOperationFilterInput>;
  amtRcdNotInvoiced?: InputMaybe<DecimalOperationFilterInput>;
  amtRcdNotInvoicedLCY?: InputMaybe<DecimalOperationFilterInput>;
  and?: InputMaybe<Array<PurchaseLineFilterInput>>;
  applToItemEntry?: InputMaybe<IntOperationFilterInput>;
  appliesToIDDelivery?: InputMaybe<StringOperationFilterInput>;
  appliesToIDReceipt?: InputMaybe<StringOperationFilterInput>;
  area?: InputMaybe<StringOperationFilterInput>;
  assessableValue?: InputMaybe<DecimalOperationFilterInput>;
  assesseeCode?: InputMaybe<StringOperationFilterInput>;
  attachedToLineNo?: InputMaybe<IntOperationFilterInput>;
  balTDSIncludingSHECESS?: InputMaybe<DecimalOperationFilterInput>;
  bcdAmount?: InputMaybe<DecimalOperationFilterInput>;
  bedAmount?: InputMaybe<DecimalOperationFilterInput>;
  binCode?: InputMaybe<StringOperationFilterInput>;
  blanketOrderLineNo?: InputMaybe<IntOperationFilterInput>;
  blanketOrderNo?: InputMaybe<StringOperationFilterInput>;
  budgetedFANo?: InputMaybe<StringOperationFilterInput>;
  button?: InputMaybe<StringOperationFilterInput>;
  buyFromVendorNo?: InputMaybe<StringOperationFilterInput>;
  capitalItem?: InputMaybe<ByteOperationFilterInput>;
  casingCondition?: InputMaybe<IntOperationFilterInput>;
  casingFreight?: InputMaybe<DecimalOperationFilterInput>;
  cessAmount?: InputMaybe<DecimalOperationFilterInput>;
  chargesToVendor?: InputMaybe<DecimalOperationFilterInput>;
  cifAmount?: InputMaybe<DecimalOperationFilterInput>;
  completelyReceived?: InputMaybe<ByteOperationFilterInput>;
  componentItemNo?: InputMaybe<StringOperationFilterInput>;
  concessionalCode?: InputMaybe<StringOperationFilterInput>;
  crossReferenceNo?: InputMaybe<StringOperationFilterInput>;
  crossReferenceType?: InputMaybe<IntOperationFilterInput>;
  crossReferenceTypeNo?: InputMaybe<StringOperationFilterInput>;
  ctshNo?: InputMaybe<StringOperationFilterInput>;
  currencyCode?: InputMaybe<StringOperationFilterInput>;
  customDutyAmount?: InputMaybe<DecimalOperationFilterInput>;
  customEcessAmount?: InputMaybe<DecimalOperationFilterInput>;
  customShecessAmount?: InputMaybe<DecimalOperationFilterInput>;
  cvd?: InputMaybe<ByteOperationFilterInput>;
  cwipglType?: InputMaybe<IntOperationFilterInput>;
  deliverCompFor?: InputMaybe<DecimalOperationFilterInput>;
  deliveryChallanDate?: InputMaybe<DateTimeOperationFilterInput>;
  deprAcquisitionCost?: InputMaybe<ByteOperationFilterInput>;
  deprUntilFAPostingDate?: InputMaybe<ByteOperationFilterInput>;
  depreciationBookCode?: InputMaybe<StringOperationFilterInput>;
  description?: InputMaybe<StringOperationFilterInput>;
  description2?: InputMaybe<StringOperationFilterInput>;
  dimensionSetID?: InputMaybe<IntOperationFilterInput>;
  directUnitCost?: InputMaybe<DecimalOperationFilterInput>;
  dispatchDate?: InputMaybe<DateTimeOperationFilterInput>;
  dispatchDestination?: InputMaybe<StringOperationFilterInput>;
  dispatchMobileNo?: InputMaybe<StringOperationFilterInput>;
  dispatchOrderNo?: InputMaybe<StringOperationFilterInput>;
  dispatchTransporter?: InputMaybe<StringOperationFilterInput>;
  dispatchVehicleNo?: InputMaybe<StringOperationFilterInput>;
  documentNo?: InputMaybe<StringOperationFilterInput>;
  documentType?: InputMaybe<IntOperationFilterInput>;
  dropShipment?: InputMaybe<ByteOperationFilterInput>;
  duplicateInDepreciationBook?: InputMaybe<StringOperationFilterInput>;
  ecessAmount?: InputMaybe<DecimalOperationFilterInput>;
  ecessOnTDS?: InputMaybe<DecimalOperationFilterInput>;
  ecessOnTDSAmount?: InputMaybe<DecimalOperationFilterInput>;
  entryPoint?: InputMaybe<StringOperationFilterInput>;
  exciseAccountingType?: InputMaybe<IntOperationFilterInput>;
  exciseAmount?: InputMaybe<DecimalOperationFilterInput>;
  exciseBaseAmount?: InputMaybe<DecimalOperationFilterInput>;
  exciseBaseQuantity?: InputMaybe<DecimalOperationFilterInput>;
  exciseBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  exciseCreditReversal?: InputMaybe<ByteOperationFilterInput>;
  exciseLoadingOnInventory?: InputMaybe<ByteOperationFilterInput>;
  exciseProdPostingGroup?: InputMaybe<StringOperationFilterInput>;
  exciseRefund?: InputMaybe<ByteOperationFilterInput>;
  exempted?: InputMaybe<ByteOperationFilterInput>;
  expectedReceiptDate?: InputMaybe<DateTimeOperationFilterInput>;
  faNo?: InputMaybe<StringOperationFilterInput>;
  faPostingDate?: InputMaybe<DateTimeOperationFilterInput>;
  faPostingType?: InputMaybe<IntOperationFilterInput>;
  factCasingCondition?: InputMaybe<IntOperationFilterInput>;
  factInspectionDate?: InputMaybe<DateTimeOperationFilterInput>;
  factInspector?: InputMaybe<StringOperationFilterInput>;
  factInspectorFinal?: InputMaybe<StringOperationFilterInput>;
  finished?: InputMaybe<ByteOperationFilterInput>;
  formCode?: InputMaybe<StringOperationFilterInput>;
  formNo?: InputMaybe<StringOperationFilterInput>;
  genBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  genProdPostingGroup?: InputMaybe<StringOperationFilterInput>;
  grossWeight?: InputMaybe<DecimalOperationFilterInput>;
  gst?: InputMaybe<DecimalOperationFilterInput>;
  gstBaseAmount?: InputMaybe<DecimalOperationFilterInput>;
  gstCredit?: InputMaybe<IntOperationFilterInput>;
  gstGroupCode?: InputMaybe<StringOperationFilterInput>;
  gstGroupType?: InputMaybe<IntOperationFilterInput>;
  gstJurisdictionType?: InputMaybe<IntOperationFilterInput>;
  hsnsacCode?: InputMaybe<StringOperationFilterInput>;
  icPartnerCode?: InputMaybe<StringOperationFilterInput>;
  icPartnerRefType?: InputMaybe<IntOperationFilterInput>;
  icPartnerReference?: InputMaybe<StringOperationFilterInput>;
  inboundWhseHandlingTime?: InputMaybe<StringOperationFilterInput>;
  indirectCost?: InputMaybe<DecimalOperationFilterInput>;
  inputTaxCreditAmount?: InputMaybe<DecimalOperationFilterInput>;
  inspection?: InputMaybe<StringOperationFilterInput>;
  inspector?: InputMaybe<StringOperationFilterInput>;
  insuranceNo?: InputMaybe<StringOperationFilterInput>;
  invDiscAmountToInvoice?: InputMaybe<DecimalOperationFilterInput>;
  invDiscountAmount?: InputMaybe<DecimalOperationFilterInput>;
  itemCategoryCode?: InputMaybe<StringOperationFilterInput>;
  itemChargeEntry?: InputMaybe<ByteOperationFilterInput>;
  jobCurrencyCode?: InputMaybe<StringOperationFilterInput>;
  jobCurrencyFactor?: InputMaybe<DecimalOperationFilterInput>;
  jobLineAmount?: InputMaybe<DecimalOperationFilterInput>;
  jobLineAmountLCY?: InputMaybe<DecimalOperationFilterInput>;
  jobLineDiscAmountLCY?: InputMaybe<DecimalOperationFilterInput>;
  jobLineDiscount?: InputMaybe<DecimalOperationFilterInput>;
  jobLineDiscountAmount?: InputMaybe<DecimalOperationFilterInput>;
  jobLineType?: InputMaybe<IntOperationFilterInput>;
  jobNo?: InputMaybe<StringOperationFilterInput>;
  jobPlanningLineNo?: InputMaybe<IntOperationFilterInput>;
  jobRemainingQty?: InputMaybe<DecimalOperationFilterInput>;
  jobRemainingQtyBase?: InputMaybe<DecimalOperationFilterInput>;
  jobTaskNo?: InputMaybe<StringOperationFilterInput>;
  jobTotalPrice?: InputMaybe<DecimalOperationFilterInput>;
  jobTotalPriceLCY?: InputMaybe<DecimalOperationFilterInput>;
  jobUnitPrice?: InputMaybe<DecimalOperationFilterInput>;
  jobUnitPriceLCY?: InputMaybe<DecimalOperationFilterInput>;
  leadTimeCalculation?: InputMaybe<StringOperationFilterInput>;
  lineAmount?: InputMaybe<DecimalOperationFilterInput>;
  lineDiscount?: InputMaybe<DecimalOperationFilterInput>;
  lineDiscountAmount?: InputMaybe<DecimalOperationFilterInput>;
  lineNo?: InputMaybe<IntOperationFilterInput>;
  locationCode?: InputMaybe<StringOperationFilterInput>;
  lug?: InputMaybe<IntOperationFilterInput>;
  maintenanceCode?: InputMaybe<StringOperationFilterInput>;
  make?: InputMaybe<StringOperationFilterInput>;
  model?: InputMaybe<StringOperationFilterInput>;
  mpsOrder?: InputMaybe<ByteOperationFilterInput>;
  nccdAmount?: InputMaybe<DecimalOperationFilterInput>;
  netWeight?: InputMaybe<DecimalOperationFilterInput>;
  newSerialNo?: InputMaybe<StringOperationFilterInput>;
  no?: InputMaybe<StringOperationFilterInput>;
  noOfCuts?: InputMaybe<IntOperationFilterInput>;
  noOfRolls?: InputMaybe<IntOperationFilterInput>;
  noOfStrips?: InputMaybe<IntOperationFilterInput>;
  nonITCClaimableUsage?: InputMaybe<DecimalOperationFilterInput>;
  nonstock?: InputMaybe<ByteOperationFilterInput>;
  notificationNo?: InputMaybe<StringOperationFilterInput>;
  notificationSlNo?: InputMaybe<StringOperationFilterInput>;
  operationNo?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<PurchaseLineFilterInput>>;
  orderDate?: InputMaybe<DateTimeOperationFilterInput>;
  orderStatus?: InputMaybe<IntOperationFilterInput>;
  outstandingAmount?: InputMaybe<DecimalOperationFilterInput>;
  outstandingAmountLCY?: InputMaybe<DecimalOperationFilterInput>;
  outstandingAmtExVATLCY?: InputMaybe<DecimalOperationFilterInput>;
  outstandingQtyBase?: InputMaybe<DecimalOperationFilterInput>;
  outstandingQuantity?: InputMaybe<DecimalOperationFilterInput>;
  overheadRate?: InputMaybe<DecimalOperationFilterInput>;
  pattern?: InputMaybe<StringOperationFilterInput>;
  payToVendorNo?: InputMaybe<StringOperationFilterInput>;
  perContract?: InputMaybe<ByteOperationFilterInput>;
  plannedReceiptDate?: InputMaybe<DateTimeOperationFilterInput>;
  planningFlexibility?: InputMaybe<IntOperationFilterInput>;
  ply?: InputMaybe<IntOperationFilterInput>;
  postingDate?: InputMaybe<DateTimeOperationFilterInput>;
  postingGroup?: InputMaybe<StringOperationFilterInput>;
  prepayment?: InputMaybe<DecimalOperationFilterInput>;
  prepaymentAmount?: InputMaybe<DecimalOperationFilterInput>;
  prepaymentLine?: InputMaybe<ByteOperationFilterInput>;
  prepaymentTaxAreaCode?: InputMaybe<StringOperationFilterInput>;
  prepaymentTaxGroupCode?: InputMaybe<StringOperationFilterInput>;
  prepaymentTaxLiable?: InputMaybe<ByteOperationFilterInput>;
  prepaymentVAT?: InputMaybe<DecimalOperationFilterInput>;
  prepaymentVATDifference?: InputMaybe<DecimalOperationFilterInput>;
  prepaymentVATIdentifier?: InputMaybe<StringOperationFilterInput>;
  prepmtAmountInvInclVAT?: InputMaybe<DecimalOperationFilterInput>;
  prepmtAmountInvLCY?: InputMaybe<DecimalOperationFilterInput>;
  prepmtAmtDeducted?: InputMaybe<DecimalOperationFilterInput>;
  prepmtAmtInclVAT?: InputMaybe<DecimalOperationFilterInput>;
  prepmtAmtInv?: InputMaybe<DecimalOperationFilterInput>;
  prepmtAmtToDeduct?: InputMaybe<DecimalOperationFilterInput>;
  prepmtLineAmount?: InputMaybe<DecimalOperationFilterInput>;
  prepmtVATAmountInvLCY?: InputMaybe<DecimalOperationFilterInput>;
  prepmtVATBaseAmt?: InputMaybe<DecimalOperationFilterInput>;
  prepmtVATCalcType?: InputMaybe<IntOperationFilterInput>;
  prepmtVATDiffDeducted?: InputMaybe<DecimalOperationFilterInput>;
  prepmtVATDiffToDeduct?: InputMaybe<DecimalOperationFilterInput>;
  procOrderNo?: InputMaybe<StringOperationFilterInput>;
  prodOrderLineNo?: InputMaybe<IntOperationFilterInput>;
  prodOrderNo?: InputMaybe<StringOperationFilterInput>;
  productGroupCode?: InputMaybe<StringOperationFilterInput>;
  profit?: InputMaybe<DecimalOperationFilterInput>;
  promisedReceiptDate?: InputMaybe<DateTimeOperationFilterInput>;
  purOrderNo?: InputMaybe<StringOperationFilterInput>;
  purchasingCode?: InputMaybe<StringOperationFilterInput>;
  qtyInvoicedBase?: InputMaybe<DecimalOperationFilterInput>;
  qtyPerUnitOfMeasure?: InputMaybe<DecimalOperationFilterInput>;
  qtyRcdNotInvoiced?: InputMaybe<DecimalOperationFilterInput>;
  qtyRcdNotInvoicedBase?: InputMaybe<DecimalOperationFilterInput>;
  qtyReceivedBase?: InputMaybe<DecimalOperationFilterInput>;
  qtyRejectedCE?: InputMaybe<DecimalOperationFilterInput>;
  qtyRejectedRework?: InputMaybe<DecimalOperationFilterInput>;
  qtyRejectedVE?: InputMaybe<DecimalOperationFilterInput>;
  qtyToInvoice?: InputMaybe<DecimalOperationFilterInput>;
  qtyToInvoiceBase?: InputMaybe<DecimalOperationFilterInput>;
  qtyToReceive?: InputMaybe<DecimalOperationFilterInput>;
  qtyToReceiveBase?: InputMaybe<DecimalOperationFilterInput>;
  qtyToRejectCE?: InputMaybe<DecimalOperationFilterInput>;
  qtyToRejectRework?: InputMaybe<DecimalOperationFilterInput>;
  qtyToRejectVE?: InputMaybe<DecimalOperationFilterInput>;
  quantity?: InputMaybe<DecimalOperationFilterInput>;
  quantityBase?: InputMaybe<DecimalOperationFilterInput>;
  quantityInvoiced?: InputMaybe<DecimalOperationFilterInput>;
  quantityReceived?: InputMaybe<DecimalOperationFilterInput>;
  reasonCode?: InputMaybe<StringOperationFilterInput>;
  receiptDate?: InputMaybe<DateTimeOperationFilterInput>;
  receiptLineNo?: InputMaybe<IntOperationFilterInput>;
  receiptNo?: InputMaybe<StringOperationFilterInput>;
  receiverID?: InputMaybe<StringOperationFilterInput>;
  rejectionReason?: InputMaybe<StringOperationFilterInput>;
  releasedProductionOrder?: InputMaybe<StringOperationFilterInput>;
  requestedReceiptDate?: InputMaybe<DateTimeOperationFilterInput>;
  responsibilityCenter?: InputMaybe<StringOperationFilterInput>;
  retQtyShpdNotInvdBase?: InputMaybe<DecimalOperationFilterInput>;
  returnQtyShipped?: InputMaybe<DecimalOperationFilterInput>;
  returnQtyShippedBase?: InputMaybe<DecimalOperationFilterInput>;
  returnQtyShippedNotInvd?: InputMaybe<DecimalOperationFilterInput>;
  returnQtyToShip?: InputMaybe<DecimalOperationFilterInput>;
  returnQtyToShipBase?: InputMaybe<DecimalOperationFilterInput>;
  returnReasonCode?: InputMaybe<StringOperationFilterInput>;
  returnShipmentLineNo?: InputMaybe<IntOperationFilterInput>;
  returnShipmentNo?: InputMaybe<StringOperationFilterInput>;
  returnShpdNotInvd?: InputMaybe<DecimalOperationFilterInput>;
  returnShpdNotInvdLCY?: InputMaybe<DecimalOperationFilterInput>;
  routingNo?: InputMaybe<StringOperationFilterInput>;
  routingReferenceNo?: InputMaybe<IntOperationFilterInput>;
  sTaxAmountIntm?: InputMaybe<DecimalOperationFilterInput>;
  sTaxBaseAmountIntm?: InputMaybe<DecimalOperationFilterInput>;
  sTaxEcessAmountIntm?: InputMaybe<DecimalOperationFilterInput>;
  sTaxSHECessAmountIntm?: InputMaybe<DecimalOperationFilterInput>;
  saedAmount?: InputMaybe<DecimalOperationFilterInput>;
  safetyLeadTime?: InputMaybe<StringOperationFilterInput>;
  salesOrderLineNo?: InputMaybe<IntOperationFilterInput>;
  salesOrderNo?: InputMaybe<StringOperationFilterInput>;
  salvageValue?: InputMaybe<DecimalOperationFilterInput>;
  scrapTyre?: InputMaybe<ByteOperationFilterInput>;
  sedAmount?: InputMaybe<DecimalOperationFilterInput>;
  segmentCode?: InputMaybe<StringOperationFilterInput>;
  sendforrework?: InputMaybe<ByteOperationFilterInput>;
  serialNo?: InputMaybe<StringOperationFilterInput>;
  serviceTaxAmount?: InputMaybe<DecimalOperationFilterInput>;
  serviceTaxBase?: InputMaybe<DecimalOperationFilterInput>;
  serviceTaxEcessAmount?: InputMaybe<DecimalOperationFilterInput>;
  serviceTaxGroup?: InputMaybe<StringOperationFilterInput>;
  serviceTaxRegistrationNo?: InputMaybe<StringOperationFilterInput>;
  serviceTaxSHECessAmount?: InputMaybe<DecimalOperationFilterInput>;
  setoffAvailable?: InputMaybe<ByteOperationFilterInput>;
  sheCessAmount?: InputMaybe<DecimalOperationFilterInput>;
  sheCessOnTDS?: InputMaybe<DecimalOperationFilterInput>;
  sheCessOnTDSAmount?: InputMaybe<DecimalOperationFilterInput>;
  shortcutDimension1Code?: InputMaybe<StringOperationFilterInput>;
  shortcutDimension2Code?: InputMaybe<StringOperationFilterInput>;
  skipNewNumber?: InputMaybe<ByteOperationFilterInput>;
  sourceDocumentNo?: InputMaybe<StringOperationFilterInput>;
  sourceDocumentType?: InputMaybe<IntOperationFilterInput>;
  specialOrder?: InputMaybe<ByteOperationFilterInput>;
  specialOrderSalesLineNo?: InputMaybe<IntOperationFilterInput>;
  specialOrderSalesNo?: InputMaybe<StringOperationFilterInput>;
  stateCode?: InputMaybe<StringOperationFilterInput>;
  status?: InputMaybe<IntOperationFilterInput>;
  subMake?: InputMaybe<StringOperationFilterInput>;
  subconreceive?: InputMaybe<ByteOperationFilterInput>;
  subconsend?: InputMaybe<ByteOperationFilterInput>;
  subcontracting?: InputMaybe<ByteOperationFilterInput>;
  supplementary?: InputMaybe<ByteOperationFilterInput>;
  surcharge?: InputMaybe<DecimalOperationFilterInput>;
  surchargeAmount?: InputMaybe<DecimalOperationFilterInput>;
  surchargeBaseAmount?: InputMaybe<DecimalOperationFilterInput>;
  systemCreatedEntry?: InputMaybe<ByteOperationFilterInput>;
  tax?: InputMaybe<DecimalOperationFilterInput>;
  taxAmount?: InputMaybe<DecimalOperationFilterInput>;
  taxAreaCode?: InputMaybe<StringOperationFilterInput>;
  taxBaseAmount?: InputMaybe<DecimalOperationFilterInput>;
  taxGroupCode?: InputMaybe<StringOperationFilterInput>;
  taxLiable?: InputMaybe<ByteOperationFilterInput>;
  tcsOnPurchase?: InputMaybe<DecimalOperationFilterInput>;
  tcsOnPurchase2?: InputMaybe<DecimalOperationFilterInput>;
  tds?: InputMaybe<DecimalOperationFilterInput>;
  tdsAmount?: InputMaybe<DecimalOperationFilterInput>;
  tdsAmountIncludingSurcharge?: InputMaybe<DecimalOperationFilterInput>;
  tdsBaseAmount?: InputMaybe<DecimalOperationFilterInput>;
  tdsCategory?: InputMaybe<IntOperationFilterInput>;
  tdsGroup?: InputMaybe<IntOperationFilterInput>;
  tdsNatureOfDeduction?: InputMaybe<StringOperationFilterInput>;
  tempTDSBase?: InputMaybe<DecimalOperationFilterInput>;
  totServTaxAmountIntm?: InputMaybe<DecimalOperationFilterInput>;
  totalGSTAmount?: InputMaybe<DecimalOperationFilterInput>;
  totalTDSIncludingSHECESS?: InputMaybe<DecimalOperationFilterInput>;
  transactionSpecification?: InputMaybe<StringOperationFilterInput>;
  transactionType?: InputMaybe<StringOperationFilterInput>;
  transportMethod?: InputMaybe<StringOperationFilterInput>;
  type?: InputMaybe<IntOperationFilterInput>;
  unitCost?: InputMaybe<DecimalOperationFilterInput>;
  unitCostLCY?: InputMaybe<DecimalOperationFilterInput>;
  unitOfMeasure?: InputMaybe<StringOperationFilterInput>;
  unitOfMeasureCode?: InputMaybe<StringOperationFilterInput>;
  unitOfMeasureCrossRef?: InputMaybe<StringOperationFilterInput>;
  unitPriceLCY?: InputMaybe<DecimalOperationFilterInput>;
  unitVolume?: InputMaybe<DecimalOperationFilterInput>;
  unitsPerParcel?: InputMaybe<DecimalOperationFilterInput>;
  useDuplicationList?: InputMaybe<ByteOperationFilterInput>;
  useTax?: InputMaybe<ByteOperationFilterInput>;
  variantCode?: InputMaybe<StringOperationFilterInput>;
  vat?: InputMaybe<DecimalOperationFilterInput>;
  vatAblePurchaseTaxAmount?: InputMaybe<DecimalOperationFilterInput>;
  vatBaseAmount?: InputMaybe<DecimalOperationFilterInput>;
  vatBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  vatCalculationType?: InputMaybe<IntOperationFilterInput>;
  vatDifference?: InputMaybe<DecimalOperationFilterInput>;
  vatIdentifier?: InputMaybe<StringOperationFilterInput>;
  vatProdPostingGroup?: InputMaybe<StringOperationFilterInput>;
  vatType?: InputMaybe<IntOperationFilterInput>;
  vendorItemNo?: InputMaybe<StringOperationFilterInput>;
  vendorShipmentNo?: InputMaybe<StringOperationFilterInput>;
  weightPerRoll?: InputMaybe<DecimalOperationFilterInput>;
  weightPerStrip?: InputMaybe<DecimalOperationFilterInput>;
  workCenterNo?: InputMaybe<StringOperationFilterInput>;
  workTax?: InputMaybe<DecimalOperationFilterInput>;
  workTaxAmount?: InputMaybe<DecimalOperationFilterInput>;
  workTaxBaseAmount?: InputMaybe<DecimalOperationFilterInput>;
  workTaxGroup?: InputMaybe<IntOperationFilterInput>;
  workTaxNatureOfDeduction?: InputMaybe<StringOperationFilterInput>;
};

export type PurchaseLineSortInput = {
  aRcdNotInvExVATLCY?: InputMaybe<SortEnumType>;
  adcvatAmount?: InputMaybe<SortEnumType>;
  adeAmount?: InputMaybe<SortEnumType>;
  adetAmount?: InputMaybe<SortEnumType>;
  aedgsiAmount?: InputMaybe<SortEnumType>;
  aedttaAmount?: InputMaybe<SortEnumType>;
  allowInvoiceDisc?: InputMaybe<SortEnumType>;
  allowItemChargeAssignment?: InputMaybe<SortEnumType>;
  amount?: InputMaybe<SortEnumType>;
  amountAddedToExciseBase?: InputMaybe<SortEnumType>;
  amountAddedToInventory?: InputMaybe<SortEnumType>;
  amountAddedToTaxBase?: InputMaybe<SortEnumType>;
  amountIncludingExcise?: InputMaybe<SortEnumType>;
  amountIncludingTax?: InputMaybe<SortEnumType>;
  amountIncludingVAT?: InputMaybe<SortEnumType>;
  amountLoadedOnInventory?: InputMaybe<SortEnumType>;
  amountToVendor?: InputMaybe<SortEnumType>;
  amtInclServiceTaxIntm?: InputMaybe<SortEnumType>;
  amtRcdNotInvoiced?: InputMaybe<SortEnumType>;
  amtRcdNotInvoicedLCY?: InputMaybe<SortEnumType>;
  applToItemEntry?: InputMaybe<SortEnumType>;
  appliesToIDDelivery?: InputMaybe<SortEnumType>;
  appliesToIDReceipt?: InputMaybe<SortEnumType>;
  area?: InputMaybe<SortEnumType>;
  assessableValue?: InputMaybe<SortEnumType>;
  assesseeCode?: InputMaybe<SortEnumType>;
  attachedToLineNo?: InputMaybe<SortEnumType>;
  balTDSIncludingSHECESS?: InputMaybe<SortEnumType>;
  bcdAmount?: InputMaybe<SortEnumType>;
  bedAmount?: InputMaybe<SortEnumType>;
  binCode?: InputMaybe<SortEnumType>;
  blanketOrderLineNo?: InputMaybe<SortEnumType>;
  blanketOrderNo?: InputMaybe<SortEnumType>;
  budgetedFANo?: InputMaybe<SortEnumType>;
  button?: InputMaybe<SortEnumType>;
  buyFromVendorNo?: InputMaybe<SortEnumType>;
  capitalItem?: InputMaybe<SortEnumType>;
  casingCondition?: InputMaybe<SortEnumType>;
  casingFreight?: InputMaybe<SortEnumType>;
  cessAmount?: InputMaybe<SortEnumType>;
  chargesToVendor?: InputMaybe<SortEnumType>;
  cifAmount?: InputMaybe<SortEnumType>;
  completelyReceived?: InputMaybe<SortEnumType>;
  componentItemNo?: InputMaybe<SortEnumType>;
  concessionalCode?: InputMaybe<SortEnumType>;
  crossReferenceNo?: InputMaybe<SortEnumType>;
  crossReferenceType?: InputMaybe<SortEnumType>;
  crossReferenceTypeNo?: InputMaybe<SortEnumType>;
  ctshNo?: InputMaybe<SortEnumType>;
  currencyCode?: InputMaybe<SortEnumType>;
  customDutyAmount?: InputMaybe<SortEnumType>;
  customEcessAmount?: InputMaybe<SortEnumType>;
  customShecessAmount?: InputMaybe<SortEnumType>;
  cvd?: InputMaybe<SortEnumType>;
  cwipglType?: InputMaybe<SortEnumType>;
  deliverCompFor?: InputMaybe<SortEnumType>;
  deliveryChallanDate?: InputMaybe<SortEnumType>;
  deprAcquisitionCost?: InputMaybe<SortEnumType>;
  deprUntilFAPostingDate?: InputMaybe<SortEnumType>;
  depreciationBookCode?: InputMaybe<SortEnumType>;
  description?: InputMaybe<SortEnumType>;
  description2?: InputMaybe<SortEnumType>;
  dimensionSetID?: InputMaybe<SortEnumType>;
  directUnitCost?: InputMaybe<SortEnumType>;
  dispatchDate?: InputMaybe<SortEnumType>;
  dispatchDestination?: InputMaybe<SortEnumType>;
  dispatchMobileNo?: InputMaybe<SortEnumType>;
  dispatchOrderNo?: InputMaybe<SortEnumType>;
  dispatchTransporter?: InputMaybe<SortEnumType>;
  dispatchVehicleNo?: InputMaybe<SortEnumType>;
  documentNo?: InputMaybe<SortEnumType>;
  documentType?: InputMaybe<SortEnumType>;
  dropShipment?: InputMaybe<SortEnumType>;
  duplicateInDepreciationBook?: InputMaybe<SortEnumType>;
  ecessAmount?: InputMaybe<SortEnumType>;
  ecessOnTDS?: InputMaybe<SortEnumType>;
  ecessOnTDSAmount?: InputMaybe<SortEnumType>;
  entryPoint?: InputMaybe<SortEnumType>;
  exciseAccountingType?: InputMaybe<SortEnumType>;
  exciseAmount?: InputMaybe<SortEnumType>;
  exciseBaseAmount?: InputMaybe<SortEnumType>;
  exciseBaseQuantity?: InputMaybe<SortEnumType>;
  exciseBusPostingGroup?: InputMaybe<SortEnumType>;
  exciseCreditReversal?: InputMaybe<SortEnumType>;
  exciseLoadingOnInventory?: InputMaybe<SortEnumType>;
  exciseProdPostingGroup?: InputMaybe<SortEnumType>;
  exciseRefund?: InputMaybe<SortEnumType>;
  exempted?: InputMaybe<SortEnumType>;
  expectedReceiptDate?: InputMaybe<SortEnumType>;
  faNo?: InputMaybe<SortEnumType>;
  faPostingDate?: InputMaybe<SortEnumType>;
  faPostingType?: InputMaybe<SortEnumType>;
  factCasingCondition?: InputMaybe<SortEnumType>;
  factInspectionDate?: InputMaybe<SortEnumType>;
  factInspector?: InputMaybe<SortEnumType>;
  factInspectorFinal?: InputMaybe<SortEnumType>;
  finished?: InputMaybe<SortEnumType>;
  formCode?: InputMaybe<SortEnumType>;
  formNo?: InputMaybe<SortEnumType>;
  genBusPostingGroup?: InputMaybe<SortEnumType>;
  genProdPostingGroup?: InputMaybe<SortEnumType>;
  grossWeight?: InputMaybe<SortEnumType>;
  gst?: InputMaybe<SortEnumType>;
  gstBaseAmount?: InputMaybe<SortEnumType>;
  gstCredit?: InputMaybe<SortEnumType>;
  gstGroupCode?: InputMaybe<SortEnumType>;
  gstGroupType?: InputMaybe<SortEnumType>;
  gstJurisdictionType?: InputMaybe<SortEnumType>;
  hsnsacCode?: InputMaybe<SortEnumType>;
  icPartnerCode?: InputMaybe<SortEnumType>;
  icPartnerRefType?: InputMaybe<SortEnumType>;
  icPartnerReference?: InputMaybe<SortEnumType>;
  inboundWhseHandlingTime?: InputMaybe<SortEnumType>;
  indirectCost?: InputMaybe<SortEnumType>;
  inputTaxCreditAmount?: InputMaybe<SortEnumType>;
  inspection?: InputMaybe<SortEnumType>;
  inspector?: InputMaybe<SortEnumType>;
  insuranceNo?: InputMaybe<SortEnumType>;
  invDiscAmountToInvoice?: InputMaybe<SortEnumType>;
  invDiscountAmount?: InputMaybe<SortEnumType>;
  itemCategoryCode?: InputMaybe<SortEnumType>;
  itemChargeEntry?: InputMaybe<SortEnumType>;
  jobCurrencyCode?: InputMaybe<SortEnumType>;
  jobCurrencyFactor?: InputMaybe<SortEnumType>;
  jobLineAmount?: InputMaybe<SortEnumType>;
  jobLineAmountLCY?: InputMaybe<SortEnumType>;
  jobLineDiscAmountLCY?: InputMaybe<SortEnumType>;
  jobLineDiscount?: InputMaybe<SortEnumType>;
  jobLineDiscountAmount?: InputMaybe<SortEnumType>;
  jobLineType?: InputMaybe<SortEnumType>;
  jobNo?: InputMaybe<SortEnumType>;
  jobPlanningLineNo?: InputMaybe<SortEnumType>;
  jobRemainingQty?: InputMaybe<SortEnumType>;
  jobRemainingQtyBase?: InputMaybe<SortEnumType>;
  jobTaskNo?: InputMaybe<SortEnumType>;
  jobTotalPrice?: InputMaybe<SortEnumType>;
  jobTotalPriceLCY?: InputMaybe<SortEnumType>;
  jobUnitPrice?: InputMaybe<SortEnumType>;
  jobUnitPriceLCY?: InputMaybe<SortEnumType>;
  leadTimeCalculation?: InputMaybe<SortEnumType>;
  lineAmount?: InputMaybe<SortEnumType>;
  lineDiscount?: InputMaybe<SortEnumType>;
  lineDiscountAmount?: InputMaybe<SortEnumType>;
  lineNo?: InputMaybe<SortEnumType>;
  locationCode?: InputMaybe<SortEnumType>;
  lug?: InputMaybe<SortEnumType>;
  maintenanceCode?: InputMaybe<SortEnumType>;
  make?: InputMaybe<SortEnumType>;
  model?: InputMaybe<SortEnumType>;
  mpsOrder?: InputMaybe<SortEnumType>;
  nccdAmount?: InputMaybe<SortEnumType>;
  netWeight?: InputMaybe<SortEnumType>;
  newSerialNo?: InputMaybe<SortEnumType>;
  no?: InputMaybe<SortEnumType>;
  noOfCuts?: InputMaybe<SortEnumType>;
  noOfRolls?: InputMaybe<SortEnumType>;
  noOfStrips?: InputMaybe<SortEnumType>;
  nonITCClaimableUsage?: InputMaybe<SortEnumType>;
  nonstock?: InputMaybe<SortEnumType>;
  notificationNo?: InputMaybe<SortEnumType>;
  notificationSlNo?: InputMaybe<SortEnumType>;
  operationNo?: InputMaybe<SortEnumType>;
  orderDate?: InputMaybe<SortEnumType>;
  orderStatus?: InputMaybe<SortEnumType>;
  outstandingAmount?: InputMaybe<SortEnumType>;
  outstandingAmountLCY?: InputMaybe<SortEnumType>;
  outstandingAmtExVATLCY?: InputMaybe<SortEnumType>;
  outstandingQtyBase?: InputMaybe<SortEnumType>;
  outstandingQuantity?: InputMaybe<SortEnumType>;
  overheadRate?: InputMaybe<SortEnumType>;
  pattern?: InputMaybe<SortEnumType>;
  payToVendorNo?: InputMaybe<SortEnumType>;
  perContract?: InputMaybe<SortEnumType>;
  plannedReceiptDate?: InputMaybe<SortEnumType>;
  planningFlexibility?: InputMaybe<SortEnumType>;
  ply?: InputMaybe<SortEnumType>;
  postingDate?: InputMaybe<SortEnumType>;
  postingGroup?: InputMaybe<SortEnumType>;
  prepayment?: InputMaybe<SortEnumType>;
  prepaymentAmount?: InputMaybe<SortEnumType>;
  prepaymentLine?: InputMaybe<SortEnumType>;
  prepaymentTaxAreaCode?: InputMaybe<SortEnumType>;
  prepaymentTaxGroupCode?: InputMaybe<SortEnumType>;
  prepaymentTaxLiable?: InputMaybe<SortEnumType>;
  prepaymentVAT?: InputMaybe<SortEnumType>;
  prepaymentVATDifference?: InputMaybe<SortEnumType>;
  prepaymentVATIdentifier?: InputMaybe<SortEnumType>;
  prepmtAmountInvInclVAT?: InputMaybe<SortEnumType>;
  prepmtAmountInvLCY?: InputMaybe<SortEnumType>;
  prepmtAmtDeducted?: InputMaybe<SortEnumType>;
  prepmtAmtInclVAT?: InputMaybe<SortEnumType>;
  prepmtAmtInv?: InputMaybe<SortEnumType>;
  prepmtAmtToDeduct?: InputMaybe<SortEnumType>;
  prepmtLineAmount?: InputMaybe<SortEnumType>;
  prepmtVATAmountInvLCY?: InputMaybe<SortEnumType>;
  prepmtVATBaseAmt?: InputMaybe<SortEnumType>;
  prepmtVATCalcType?: InputMaybe<SortEnumType>;
  prepmtVATDiffDeducted?: InputMaybe<SortEnumType>;
  prepmtVATDiffToDeduct?: InputMaybe<SortEnumType>;
  procOrderNo?: InputMaybe<SortEnumType>;
  prodOrderLineNo?: InputMaybe<SortEnumType>;
  prodOrderNo?: InputMaybe<SortEnumType>;
  productGroupCode?: InputMaybe<SortEnumType>;
  profit?: InputMaybe<SortEnumType>;
  promisedReceiptDate?: InputMaybe<SortEnumType>;
  purOrderNo?: InputMaybe<SortEnumType>;
  purchasingCode?: InputMaybe<SortEnumType>;
  qtyInvoicedBase?: InputMaybe<SortEnumType>;
  qtyPerUnitOfMeasure?: InputMaybe<SortEnumType>;
  qtyRcdNotInvoiced?: InputMaybe<SortEnumType>;
  qtyRcdNotInvoicedBase?: InputMaybe<SortEnumType>;
  qtyReceivedBase?: InputMaybe<SortEnumType>;
  qtyRejectedCE?: InputMaybe<SortEnumType>;
  qtyRejectedRework?: InputMaybe<SortEnumType>;
  qtyRejectedVE?: InputMaybe<SortEnumType>;
  qtyToInvoice?: InputMaybe<SortEnumType>;
  qtyToInvoiceBase?: InputMaybe<SortEnumType>;
  qtyToReceive?: InputMaybe<SortEnumType>;
  qtyToReceiveBase?: InputMaybe<SortEnumType>;
  qtyToRejectCE?: InputMaybe<SortEnumType>;
  qtyToRejectRework?: InputMaybe<SortEnumType>;
  qtyToRejectVE?: InputMaybe<SortEnumType>;
  quantity?: InputMaybe<SortEnumType>;
  quantityBase?: InputMaybe<SortEnumType>;
  quantityInvoiced?: InputMaybe<SortEnumType>;
  quantityReceived?: InputMaybe<SortEnumType>;
  reasonCode?: InputMaybe<SortEnumType>;
  receiptDate?: InputMaybe<SortEnumType>;
  receiptLineNo?: InputMaybe<SortEnumType>;
  receiptNo?: InputMaybe<SortEnumType>;
  receiverID?: InputMaybe<SortEnumType>;
  rejectionReason?: InputMaybe<SortEnumType>;
  releasedProductionOrder?: InputMaybe<SortEnumType>;
  requestedReceiptDate?: InputMaybe<SortEnumType>;
  responsibilityCenter?: InputMaybe<SortEnumType>;
  retQtyShpdNotInvdBase?: InputMaybe<SortEnumType>;
  returnQtyShipped?: InputMaybe<SortEnumType>;
  returnQtyShippedBase?: InputMaybe<SortEnumType>;
  returnQtyShippedNotInvd?: InputMaybe<SortEnumType>;
  returnQtyToShip?: InputMaybe<SortEnumType>;
  returnQtyToShipBase?: InputMaybe<SortEnumType>;
  returnReasonCode?: InputMaybe<SortEnumType>;
  returnShipmentLineNo?: InputMaybe<SortEnumType>;
  returnShipmentNo?: InputMaybe<SortEnumType>;
  returnShpdNotInvd?: InputMaybe<SortEnumType>;
  returnShpdNotInvdLCY?: InputMaybe<SortEnumType>;
  routingNo?: InputMaybe<SortEnumType>;
  routingReferenceNo?: InputMaybe<SortEnumType>;
  sTaxAmountIntm?: InputMaybe<SortEnumType>;
  sTaxBaseAmountIntm?: InputMaybe<SortEnumType>;
  sTaxEcessAmountIntm?: InputMaybe<SortEnumType>;
  sTaxSHECessAmountIntm?: InputMaybe<SortEnumType>;
  saedAmount?: InputMaybe<SortEnumType>;
  safetyLeadTime?: InputMaybe<SortEnumType>;
  salesOrderLineNo?: InputMaybe<SortEnumType>;
  salesOrderNo?: InputMaybe<SortEnumType>;
  salvageValue?: InputMaybe<SortEnumType>;
  scrapTyre?: InputMaybe<SortEnumType>;
  sedAmount?: InputMaybe<SortEnumType>;
  segmentCode?: InputMaybe<SortEnumType>;
  sendforrework?: InputMaybe<SortEnumType>;
  serialNo?: InputMaybe<SortEnumType>;
  serviceTaxAmount?: InputMaybe<SortEnumType>;
  serviceTaxBase?: InputMaybe<SortEnumType>;
  serviceTaxEcessAmount?: InputMaybe<SortEnumType>;
  serviceTaxGroup?: InputMaybe<SortEnumType>;
  serviceTaxRegistrationNo?: InputMaybe<SortEnumType>;
  serviceTaxSHECessAmount?: InputMaybe<SortEnumType>;
  setoffAvailable?: InputMaybe<SortEnumType>;
  sheCessAmount?: InputMaybe<SortEnumType>;
  sheCessOnTDS?: InputMaybe<SortEnumType>;
  sheCessOnTDSAmount?: InputMaybe<SortEnumType>;
  shortcutDimension1Code?: InputMaybe<SortEnumType>;
  shortcutDimension2Code?: InputMaybe<SortEnumType>;
  skipNewNumber?: InputMaybe<SortEnumType>;
  sourceDocumentNo?: InputMaybe<SortEnumType>;
  sourceDocumentType?: InputMaybe<SortEnumType>;
  specialOrder?: InputMaybe<SortEnumType>;
  specialOrderSalesLineNo?: InputMaybe<SortEnumType>;
  specialOrderSalesNo?: InputMaybe<SortEnumType>;
  stateCode?: InputMaybe<SortEnumType>;
  status?: InputMaybe<SortEnumType>;
  subMake?: InputMaybe<SortEnumType>;
  subconreceive?: InputMaybe<SortEnumType>;
  subconsend?: InputMaybe<SortEnumType>;
  subcontracting?: InputMaybe<SortEnumType>;
  supplementary?: InputMaybe<SortEnumType>;
  surcharge?: InputMaybe<SortEnumType>;
  surchargeAmount?: InputMaybe<SortEnumType>;
  surchargeBaseAmount?: InputMaybe<SortEnumType>;
  systemCreatedEntry?: InputMaybe<SortEnumType>;
  tax?: InputMaybe<SortEnumType>;
  taxAmount?: InputMaybe<SortEnumType>;
  taxAreaCode?: InputMaybe<SortEnumType>;
  taxBaseAmount?: InputMaybe<SortEnumType>;
  taxGroupCode?: InputMaybe<SortEnumType>;
  taxLiable?: InputMaybe<SortEnumType>;
  tcsOnPurchase?: InputMaybe<SortEnumType>;
  tcsOnPurchase2?: InputMaybe<SortEnumType>;
  tds?: InputMaybe<SortEnumType>;
  tdsAmount?: InputMaybe<SortEnumType>;
  tdsAmountIncludingSurcharge?: InputMaybe<SortEnumType>;
  tdsBaseAmount?: InputMaybe<SortEnumType>;
  tdsCategory?: InputMaybe<SortEnumType>;
  tdsGroup?: InputMaybe<SortEnumType>;
  tdsNatureOfDeduction?: InputMaybe<SortEnumType>;
  tempTDSBase?: InputMaybe<SortEnumType>;
  totServTaxAmountIntm?: InputMaybe<SortEnumType>;
  totalGSTAmount?: InputMaybe<SortEnumType>;
  totalTDSIncludingSHECESS?: InputMaybe<SortEnumType>;
  transactionSpecification?: InputMaybe<SortEnumType>;
  transactionType?: InputMaybe<SortEnumType>;
  transportMethod?: InputMaybe<SortEnumType>;
  type?: InputMaybe<SortEnumType>;
  unitCost?: InputMaybe<SortEnumType>;
  unitCostLCY?: InputMaybe<SortEnumType>;
  unitOfMeasure?: InputMaybe<SortEnumType>;
  unitOfMeasureCode?: InputMaybe<SortEnumType>;
  unitOfMeasureCrossRef?: InputMaybe<SortEnumType>;
  unitPriceLCY?: InputMaybe<SortEnumType>;
  unitVolume?: InputMaybe<SortEnumType>;
  unitsPerParcel?: InputMaybe<SortEnumType>;
  useDuplicationList?: InputMaybe<SortEnumType>;
  useTax?: InputMaybe<SortEnumType>;
  variantCode?: InputMaybe<SortEnumType>;
  vat?: InputMaybe<SortEnumType>;
  vatAblePurchaseTaxAmount?: InputMaybe<SortEnumType>;
  vatBaseAmount?: InputMaybe<SortEnumType>;
  vatBusPostingGroup?: InputMaybe<SortEnumType>;
  vatCalculationType?: InputMaybe<SortEnumType>;
  vatDifference?: InputMaybe<SortEnumType>;
  vatIdentifier?: InputMaybe<SortEnumType>;
  vatProdPostingGroup?: InputMaybe<SortEnumType>;
  vatType?: InputMaybe<SortEnumType>;
  vendorItemNo?: InputMaybe<SortEnumType>;
  vendorShipmentNo?: InputMaybe<SortEnumType>;
  weightPerRoll?: InputMaybe<SortEnumType>;
  weightPerStrip?: InputMaybe<SortEnumType>;
  workCenterNo?: InputMaybe<SortEnumType>;
  workTax?: InputMaybe<SortEnumType>;
  workTaxAmount?: InputMaybe<SortEnumType>;
  workTaxBaseAmount?: InputMaybe<SortEnumType>;
  workTaxGroup?: InputMaybe<SortEnumType>;
  workTaxNatureOfDeduction?: InputMaybe<SortEnumType>;
};

export type Query = {
  __typename?: 'Query';
  dealerByCode: SalespersonPurchaser;
  exportCalendarIcs: Scalars['String']['output'];
  getCalendarAuditLog: Array<CalendarAuditLogEntryDto>;
  getCalendarConflicts: Array<CalendarEventDto>;
  getCalendarEventById?: Maybe<CalendarEventDto>;
  getEventTypes: Array<EventTypeDto>;
  getFreeBusy: Array<FreeBusyDto>;
  getMyCalendarEvents: Array<CalendarEventDto>;
  getMyDocuments: Array<DocumentDto>;
  getNotificationPreference?: Maybe<NotificationPreferenceDto>;
  getNotifications: Array<Notification>;
  getSharedCalendars: Array<CalendarShareDto>;
  getUnreadNotificationCount: Scalars['Int']['output'];
  getUpcomingReminders: Array<CalendarEventDto>;
  groupDetails: Array<GroupDetails>;
  myAreas?: Maybe<MyAreasConnection>;
  myBalance: Array<EntityBalance>;
  myCustomers?: Maybe<MyCustomersConnection>;
  myDealers?: Maybe<MyDealersConnection>;
  myRegions?: Maybe<MyRegionsConnection>;
  myRespCenters?: Maybe<MyRespCentersConnection>;
  myTransactions?: Maybe<MyTransactionsConnection>;
  myVehicles?: Maybe<MyVehiclesConnection>;
  myVendors?: Maybe<MyVendorsConnection>;
  postCodes?: Maybe<PostCodesConnection>;
  procurementNewNumberingPaged?: Maybe<ProcurementNewNumberingPagedConnection>;
  procurementOrderLines?: Maybe<ProcurementOrderLinesConnection>;
  procurementOrders?: Maybe<ProcurementOrdersConnection>;
  productionEcomileLastNewNumber: Scalars['String']['output'];
  productionEcomileProcurementTiles: Array<Tile>;
  productionInspectorCodeNames: Array<CodeName>;
  productionItemNos: Array<CasingItem>;
  productionMakeSubMake: Array<CodeName>;
  productionMakes: Array<CodeName>;
  productionProcMarkets: Array<Scalars['String']['output']>;
  productionProcurementDispatchOrders: Array<DispatchOrder>;
  productionProcurementInspection: Array<CodeName>;
  productionProcurementMarkets: Array<CodeName>;
  productionProcurementOrderLines: Array<OrderLine>;
  productionProcurementOrderLinesDispatch: Array<OrderLineDispatch>;
  productionProcurementOrderLinesNewNumbering: Array<OrderLineDispatch>;
  productionProcurementOrdersInfo: Array<OrderInfo>;
  productionShipmentOrderForMerger: Array<ShipmentInfo>;
  productionVendors: Array<VendorModel>;
  productionVendorsCodeNames: Array<CodeName>;
  profile?: Maybe<ProfileResult>;
  purchaseItemNos?: Maybe<PurchaseItemNosConnection>;
  reportSalesMeta: Array<ReportMeta>;
  salesInvLinesForInvoice: Array<EInvoiceCandidate>;
  searchUsers: Array<UserSearchResult>;
  sessions: Array<SessionInfo>;
  user?: Maybe<UserDetail>;
  version: Scalars['String']['output'];
};


export type QueryDealerByCodeArgs = {
  code: Scalars['String']['input'];
};


export type QueryExportCalendarIcsArgs = {
  fromUtc: Scalars['DateTime']['input'];
  toUtc: Scalars['DateTime']['input'];
};


export type QueryGetCalendarAuditLogArgs = {
  eventId?: InputMaybe<Scalars['UUID']['input']>;
  limit?: Scalars['Int']['input'];
  userId?: InputMaybe<Scalars['String']['input']>;
};


export type QueryGetCalendarConflictsArgs = {
  endUtc: Scalars['DateTime']['input'];
  excludeEventId?: InputMaybe<Scalars['UUID']['input']>;
  startUtc: Scalars['DateTime']['input'];
};


export type QueryGetCalendarEventByIdArgs = {
  eventId: Scalars['UUID']['input'];
};


export type QueryGetFreeBusyArgs = {
  fromUtc: Scalars['DateTime']['input'];
  toUtc: Scalars['DateTime']['input'];
  userIds: Array<Scalars['String']['input']>;
};


export type QueryGetMyCalendarEventsArgs = {
  fromUtc: Scalars['DateTime']['input'];
  skip?: InputMaybe<Scalars['Int']['input']>;
  tagKey?: InputMaybe<Scalars['String']['input']>;
  tagType?: InputMaybe<EventTagType>;
  take?: InputMaybe<Scalars['Int']['input']>;
  toUtc: Scalars['DateTime']['input'];
};


export type QueryGetMyDocumentsArgs = {
  parameters: SalesReportParamsInput;
};


export type QueryGetNotificationsArgs = {
  limit?: Scalars['Int']['input'];
};


export type QueryGetUpcomingRemindersArgs = {
  untilUtc?: InputMaybe<Scalars['DateTime']['input']>;
};


export type QueryGroupDetailsArgs = {
  category: Scalars['String']['input'];
  codes?: InputMaybe<Scalars['String']['input']>;
};


export type QueryMyAreasArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  department?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  entityType?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<AreaSortInput>>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<AreaFilterInput>;
};


export type QueryMyBalanceArgs = {
  entityCode?: InputMaybe<Scalars['String']['input']>;
  entityType?: InputMaybe<Scalars['String']['input']>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
};


export type QueryMyCustomersArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  dealerCode?: InputMaybe<Scalars['String']['input']>;
  department?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  entityType?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<CustomerSortInput>>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<CustomerFilterInput>;
};


export type QueryMyDealersArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  department?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  entityType?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<SalespersonPurchaserSortInput>>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<SalespersonPurchaserFilterInput>;
};


export type QueryMyRegionsArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  department?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  entityType?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<TerritorySortInput>>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<TerritoryFilterInput>;
};


export type QueryMyRespCentersArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<ResponsibilityCenterSortInput>>;
  type: Scalars['String']['input'];
  where?: InputMaybe<ResponsibilityCenterFilterInput>;
};


export type QueryMyTransactionsArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  entityType?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<AccountTransactionSortInput>>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<AccountTransactionFilterInput>;
};


export type QueryMyVehiclesArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  department?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  entityType?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<VehiclesSortInput>>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<VehiclesFilterInput>;
};


export type QueryMyVendorsArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  categories?: InputMaybe<Array<Scalars['String']['input']>>;
  ecoMgr?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<VendorSortInput>>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<VendorFilterInput>;
};


export type QueryPostCodesArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<PostCodeSortInput>>;
  where?: InputMaybe<PostCodeFilterInput>;
};


export type QueryProcurementNewNumberingPagedArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  fromDate?: InputMaybe<Scalars['DateTime']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  nos?: InputMaybe<Array<Scalars['String']['input']>>;
  order?: InputMaybe<Array<ProcurementNewNumberingDtoSortInput>>;
  respCenters?: InputMaybe<Scalars['String']['input']>;
  toDate?: InputMaybe<Scalars['DateTime']['input']>;
  type?: InputMaybe<Scalars['String']['input']>;
  view?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<ProcurementNewNumberingDtoFilterInput>;
};


export type QueryProcurementOrderLinesArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<PurchaseLineSortInput>>;
  respCenters?: InputMaybe<Scalars['String']['input']>;
  statusFilter?: InputMaybe<Scalars['Int']['input']>;
  userCode?: InputMaybe<Scalars['String']['input']>;
  userDepartment?: InputMaybe<Scalars['String']['input']>;
  userSpecialToken?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<PurchaseLineFilterInput>;
};


export type QueryProcurementOrdersArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<PurchaseHeaderSortInput>>;
  respCenters?: InputMaybe<Scalars['String']['input']>;
  statusFilter?: InputMaybe<Scalars['Int']['input']>;
  userCode?: InputMaybe<Scalars['String']['input']>;
  userDepartment?: InputMaybe<Scalars['String']['input']>;
  userSpecialToken?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<PurchaseHeaderFilterInput>;
};


export type QueryProductionEcomileLastNewNumberArgs = {
  respCenter: Scalars['String']['input'];
};


export type QueryProductionEcomileProcurementTilesArgs = {
  param: FetchParamsInput;
};


export type QueryProductionInspectorCodeNamesArgs = {
  param: FetchParamsInput;
};


export type QueryProductionItemNosArgs = {
  param: FetchParamsInput;
};


export type QueryProductionMakeSubMakeArgs = {
  param: FetchParamsInput;
};


export type QueryProductionMakesArgs = {
  param: FetchParamsInput;
};


export type QueryProductionProcMarketsArgs = {
  param: FetchParamsInput;
};


export type QueryProductionProcurementDispatchOrdersArgs = {
  param: FetchParamsInput;
};


export type QueryProductionProcurementInspectionArgs = {
  param: FetchParamsInput;
};


export type QueryProductionProcurementMarketsArgs = {
  param: FetchParamsInput;
};


export type QueryProductionProcurementOrderLinesArgs = {
  param: OrderInfoInput;
};


export type QueryProductionProcurementOrderLinesDispatchArgs = {
  param: FetchParamsInput;
};


export type QueryProductionProcurementOrderLinesNewNumberingArgs = {
  param: FetchParamsInput;
};


export type QueryProductionProcurementOrdersInfoArgs = {
  param: FetchParamsInput;
};


export type QueryProductionShipmentOrderForMergerArgs = {
  param: FetchParamsInput;
};


export type QueryProductionVendorsArgs = {
  param: FetchParamsInput;
};


export type QueryProductionVendorsCodeNamesArgs = {
  param: FetchParamsInput;
};


export type QueryProfileArgs = {
  userId: Scalars['String']['input'];
};


export type QueryPurchaseItemNosArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<CasingItemSortInput>>;
  param: FetchParamsInput;
  where?: InputMaybe<CasingItemFilterInput>;
};


export type QueryReportSalesMetaArgs = {
  reports?: InputMaybe<Scalars['String']['input']>;
};


export type QuerySearchUsersArgs = {
  search?: InputMaybe<Scalars['String']['input']>;
  take?: InputMaybe<Scalars['Int']['input']>;
};


export type QuerySessionsArgs = {
  userId?: InputMaybe<Scalars['String']['input']>;
};


export type QueryUserArgs = {
  username: Scalars['String']['input'];
};

export type RecurrenceDto = {
  __typename?: 'RecurrenceDto';
  dayOfMonth?: Maybe<Scalars['Int']['output']>;
  daysOfWeek?: Maybe<Scalars['String']['output']>;
  endByDate?: Maybe<Scalars['Date']['output']>;
  frequency: RecurrenceFrequency;
  interval: Scalars['Int']['output'];
  monthOfYear?: Maybe<Scalars['Int']['output']>;
  occurrenceCount?: Maybe<Scalars['Int']['output']>;
  rRule?: Maybe<Scalars['String']['output']>;
};

export enum RecurrenceFrequency {
  Daily = 'DAILY',
  Monthly = 'MONTHLY',
  Weekly = 'WEEKLY',
  Yearly = 'YEARLY'
}

export type RecurrenceInput = {
  dayOfMonth?: InputMaybe<Scalars['Int']['input']>;
  daysOfWeek?: InputMaybe<Scalars['String']['input']>;
  endByDate?: InputMaybe<Scalars['Date']['input']>;
  frequency: RecurrenceFrequency;
  interval: Scalars['Int']['input'];
  monthOfYear?: InputMaybe<Scalars['Int']['input']>;
  occurrenceCount?: InputMaybe<Scalars['Int']['input']>;
  rRule?: InputMaybe<Scalars['String']['input']>;
};

export enum ReminderChannel {
  Email = 'EMAIL',
  InApp = 'IN_APP',
  Push = 'PUSH'
}

export type ReminderDto = {
  __typename?: 'ReminderDto';
  channel: ReminderChannel;
  id: Scalars['UUID']['output'];
  isSent: Scalars['Boolean']['output'];
  remindAtUtc: Scalars['DateTime']['output'];
  snoozeUntilUtc?: Maybe<Scalars['DateTime']['output']>;
};

export type ReminderInput = {
  channel: ReminderChannel;
  remindAtUtc: Scalars['DateTime']['input'];
};

export type ReportMeta = {
  __typename?: 'ReportMeta';
  code: Scalars['String']['output'];
  datePreset: Scalars['String']['output'];
  id: Scalars['Int']['output'];
  name: Scalars['String']['output'];
  outputFormats: Scalars['String']['output'];
  requiredFields: Scalars['String']['output'];
  showAreas: Scalars['Boolean']['output'];
  showCustomers: Scalars['Boolean']['output'];
  showDealers: Scalars['Boolean']['output'];
  showNos: Scalars['Boolean']['output'];
  showRegions: Scalars['Boolean']['output'];
  showRespCenters: Scalars['Boolean']['output'];
  showType: Scalars['Boolean']['output'];
  showView: Scalars['Boolean']['output'];
  typeOptions: Array<Scalars['String']['output']>;
  viewOptions: Array<Scalars['String']['output']>;
};

export type ResetPasswordResult = {
  __typename?: 'ResetPasswordResult';
  message: Scalars['String']['output'];
  newPassword?: Maybe<Scalars['String']['output']>;
  success: Scalars['Boolean']['output'];
};

export type ResponsibilityCenter = {
  __typename?: 'ResponsibilityCenter';
  addIncetiveInSalAdv: Scalars['Byte']['output'];
  address?: Maybe<Scalars['String']['output']>;
  address2?: Maybe<Scalars['String']['output']>;
  advanceDedCode?: Maybe<Scalars['String']['output']>;
  assCheckProdGroupQty: Scalars['Byte']['output'];
  assQtyLimit: Scalars['Decimal']['output'];
  assemblyOrderNos?: Maybe<Scalars['String']['output']>;
  attendanceBonus: Scalars['Byte']['output'];
  bankAccountNo?: Maybe<Scalars['String']['output']>;
  bonus: Scalars['Decimal']['output'];
  cinNo?: Maybe<Scalars['String']['output']>;
  city?: Maybe<Scalars['String']['output']>;
  claimOrderNos?: Maybe<Scalars['String']['output']>;
  code?: Maybe<Scalars['String']['output']>;
  companyName?: Maybe<Scalars['String']['output']>;
  contact?: Maybe<Scalars['String']['output']>;
  contactNo1?: Maybe<Scalars['String']['output']>;
  contactNo2?: Maybe<Scalars['String']['output']>;
  countryRegionCode?: Maybe<Scalars['String']['output']>;
  county?: Maybe<Scalars['String']['output']>;
  creditLimitOnSalesOrder: Scalars['Byte']['output'];
  cstNo?: Maybe<Scalars['String']['output']>;
  customerNos?: Maybe<Scalars['String']['output']>;
  dearnessAllowance: Scalars['Decimal']['output'];
  deliveryOrderNos?: Maybe<Scalars['String']['output']>;
  depotGroup?: Maybe<Scalars['String']['output']>;
  discToDealerGLEco?: Maybe<Scalars['String']['output']>;
  discToDealerGLNo?: Maybe<Scalars['String']['output']>;
  eMail?: Maybe<Scalars['String']['output']>;
  einvPassword?: Maybe<Scalars['String']['output']>;
  einvUsername?: Maybe<Scalars['String']['output']>;
  employeeNos?: Maybe<Scalars['String']['output']>;
  esicEnabled: Scalars['Byte']['output'];
  esicRoundup: Scalars['Byte']['output'];
  ewaybillEnabled: Scalars['Byte']['output'];
  ewaybillFromDate?: Maybe<Scalars['DateTime']['output']>;
  exGratia: Scalars['Decimal']['output'];
  exciseCommissionerate?: Maybe<Scalars['String']['output']>;
  exciseDivision?: Maybe<Scalars['String']['output']>;
  exciseRange?: Maybe<Scalars['String']['output']>;
  exciseRegNo?: Maybe<Scalars['String']['output']>;
  expenseGLNo?: Maybe<Scalars['String']['output']>;
  faxNo?: Maybe<Scalars['String']['output']>;
  freightGLNo?: Maybe<Scalars['String']['output']>;
  getUnpostedEmpAttendance: Scalars['Byte']['output'];
  glForTDSOnTurnoverDisc?: Maybe<Scalars['String']['output']>;
  glForTurnoverDisc?: Maybe<Scalars['String']['output']>;
  globalDimension1Code?: Maybe<Scalars['String']['output']>;
  globalDimension2Code?: Maybe<Scalars['String']['output']>;
  gstEffectingDate?: Maybe<Scalars['DateTime']['output']>;
  gstGroupDef?: Maybe<Scalars['String']['output']>;
  gstInvoiceText?: Maybe<Scalars['String']['output']>;
  gstNo?: Maybe<Scalars['String']['output']>;
  gstTradeName?: Maybe<Scalars['String']['output']>;
  homePage?: Maybe<Scalars['String']['output']>;
  hreMailAddress?: Maybe<Scalars['String']['output']>;
  hsnsacNoDef?: Maybe<Scalars['String']['output']>;
  incentiveEarnCode?: Maybe<Scalars['String']['output']>;
  inspectionRequire: Scalars['Byte']['output'];
  jobWorkSheetNos?: Maybe<Scalars['String']['output']>;
  juridiction?: Maybe<Scalars['String']['output']>;
  lastEcomileCrnSync?: Maybe<Scalars['String']['output']>;
  lastEcomileInvSync?: Maybe<Scalars['String']['output']>;
  lastEcomilePcrnSync?: Maybe<Scalars['String']['output']>;
  lastEcomilePinvSync?: Maybe<Scalars['String']['output']>;
  leaveEarningMonthly: Scalars['Byte']['output'];
  licDedCode?: Maybe<Scalars['String']['output']>;
  locationCode?: Maybe<Scalars['String']['output']>;
  logo?: Maybe<Array<Scalars['Byte']['output']>>;
  minWages: Scalars['Decimal']['output'];
  miscSalesInvoiceNos?: Maybe<Scalars['String']['output']>;
  monthDaysInBonusCalc: Scalars['Byte']['output'];
  msmeNo?: Maybe<Scalars['String']['output']>;
  name?: Maybe<Scalars['String']['output']>;
  name2?: Maybe<Scalars['String']['output']>;
  natureOfBusiness: Scalars['Int']['output'];
  navConfig?: Maybe<Scalars['String']['output']>;
  npsDedCode?: Maybe<Scalars['String']['output']>;
  onlineOrderNos?: Maybe<Scalars['String']['output']>;
  onlyRepairs: Scalars['Decimal']['output'];
  onlyRepairsDisc: Scalars['Decimal']['output'];
  otEarnCode?: Maybe<Scalars['String']['output']>;
  panNo?: Maybe<Scalars['String']['output']>;
  panOnSales: Scalars['Byte']['output'];
  partnerCode?: Maybe<Scalars['String']['output']>;
  partyNos?: Maybe<Scalars['String']['output']>;
  payroll: Scalars['Byte']['output'];
  pettyCashGLAccount?: Maybe<Scalars['String']['output']>;
  pfCalcSkipConditional: Scalars['Byte']['output'];
  pfEstCode?: Maybe<Scalars['String']['output']>;
  phoneNo?: Maybe<Scalars['String']['output']>;
  postCode?: Maybe<Scalars['String']['output']>;
  postDiscountToDealer: Scalars['Byte']['output'];
  postedAssemblyOrderNos?: Maybe<Scalars['String']['output']>;
  postedClaimOrderNos?: Maybe<Scalars['String']['output']>;
  postedDeliveryOrderNos?: Maybe<Scalars['String']['output']>;
  postedTFMOrderNos?: Maybe<Scalars['String']['output']>;
  priceReqOrderNos?: Maybe<Scalars['String']['output']>;
  procShipNos?: Maybe<Scalars['String']['output']>;
  procurementNos?: Maybe<Scalars['String']['output']>;
  prodIncAttendanceLimit: Scalars['Decimal']['output'];
  prodIncTotalUnits: Scalars['Decimal']['output'];
  prodIncentive: Scalars['Byte']['output'];
  production: Scalars['Byte']['output'];
  profTax: Scalars['Byte']['output'];
  purCrMemoNos?: Maybe<Scalars['String']['output']>;
  purCrMemoPostNos?: Maybe<Scalars['String']['output']>;
  purInvoiceNos?: Maybe<Scalars['String']['output']>;
  purInvoicePostNos?: Maybe<Scalars['String']['output']>;
  purOrderNos?: Maybe<Scalars['String']['output']>;
  purRecieptsNos?: Maybe<Scalars['String']['output']>;
  purchase: Scalars['Byte']['output'];
  regOfficeCode?: Maybe<Scalars['String']['output']>;
  respCenterSales?: Maybe<Scalars['String']['output']>;
  sale: Scalars['Byte']['output'];
  salesAmtLimitForPAN: Scalars['Decimal']['output'];
  salesClosureEndDate?: Maybe<Scalars['DateTime']['output']>;
  salesCrMemoNos?: Maybe<Scalars['String']['output']>;
  salesCrMemoPostNos?: Maybe<Scalars['String']['output']>;
  salesDrMemoNos?: Maybe<Scalars['String']['output']>;
  salesDrMemoPostNos?: Maybe<Scalars['String']['output']>;
  salesInvNosEcomile?: Maybe<Scalars['String']['output']>;
  salesInvoiceNos?: Maybe<Scalars['String']['output']>;
  salesInvoicePostNos?: Maybe<Scalars['String']['output']>;
  salesOrderNos?: Maybe<Scalars['String']['output']>;
  salesQuoteNos?: Maybe<Scalars['String']['output']>;
  salesShipmentNos?: Maybe<Scalars['String']['output']>;
  serviceTaxNo?: Maybe<Scalars['String']['output']>;
  shopCalendarCode?: Maybe<Scalars['String']['output']>;
  singlesheetdataNos?: Maybe<Scalars['String']['output']>;
  soPriceLimitBase: Scalars['Decimal']['output'];
  soPriceLimitGen: Scalars['Decimal']['output'];
  socLoanBatchName?: Maybe<Scalars['String']['output']>;
  socLoanEMIDedCode?: Maybe<Scalars['String']['output']>;
  socLoanInt: Scalars['Decimal']['output'];
  socLoanIntDedCode?: Maybe<Scalars['String']['output']>;
  socShareDedCode?: Maybe<Scalars['String']['output']>;
  staffLoanEMIDedCode?: Maybe<Scalars['String']['output']>;
  staffLoanInt: Scalars['Decimal']['output'];
  staffLoanIntDedCode?: Maybe<Scalars['String']['output']>;
  state?: Maybe<Scalars['String']['output']>;
  stopRoundupSalary: Scalars['Byte']['output'];
  supportDocMustOnCustomer: Scalars['Byte']['output'];
  supportDocMustOnDealer: Scalars['Byte']['output'];
  supportDocMustOnGRA: Scalars['Byte']['output'];
  tcsOnPurch: Scalars['Decimal']['output'];
  tcsOnSaleGLNo?: Maybe<Scalars['String']['output']>;
  tcsOnSales: Scalars['Decimal']['output'];
  tdsDedCode?: Maybe<Scalars['String']['output']>;
  tdsOnDealerEarnGLNo?: Maybe<Scalars['String']['output']>;
  tdsOnEarnNos?: Maybe<Scalars['String']['output']>;
  tdsOnTurnoverDisc: Scalars['Decimal']['output'];
  tdsRoundingUp: Scalars['Byte']['output'];
  tdsThersholdLimit: Scalars['Decimal']['output'];
  tdsWithPAN: Scalars['Decimal']['output'];
  tdsWithoutPAN: Scalars['Decimal']['output'];
  tfmOrderNos?: Maybe<Scalars['String']['output']>;
  tinNo?: Maybe<Scalars['String']['output']>;
  uheMailAddress?: Maybe<Scalars['String']['output']>;
  unitName?: Maybe<Scalars['String']['output']>;
  urdPurCrMemoPostNos?: Maybe<Scalars['String']['output']>;
  urdPurInvoicePostNos?: Maybe<Scalars['String']['output']>;
  vendorNos?: Maybe<Scalars['String']['output']>;
  yearlyBonus: Scalars['Decimal']['output'];
};

export type ResponsibilityCenterFilterInput = {
  addIncetiveInSalAdv?: InputMaybe<ByteOperationFilterInput>;
  address?: InputMaybe<StringOperationFilterInput>;
  address2?: InputMaybe<StringOperationFilterInput>;
  advanceDedCode?: InputMaybe<StringOperationFilterInput>;
  and?: InputMaybe<Array<ResponsibilityCenterFilterInput>>;
  assCheckProdGroupQty?: InputMaybe<ByteOperationFilterInput>;
  assQtyLimit?: InputMaybe<DecimalOperationFilterInput>;
  assemblyOrderNos?: InputMaybe<StringOperationFilterInput>;
  attendanceBonus?: InputMaybe<ByteOperationFilterInput>;
  bankAccountNo?: InputMaybe<StringOperationFilterInput>;
  bonus?: InputMaybe<DecimalOperationFilterInput>;
  cinNo?: InputMaybe<StringOperationFilterInput>;
  city?: InputMaybe<StringOperationFilterInput>;
  claimOrderNos?: InputMaybe<StringOperationFilterInput>;
  code?: InputMaybe<StringOperationFilterInput>;
  companyName?: InputMaybe<StringOperationFilterInput>;
  contact?: InputMaybe<StringOperationFilterInput>;
  contactNo1?: InputMaybe<StringOperationFilterInput>;
  contactNo2?: InputMaybe<StringOperationFilterInput>;
  countryRegionCode?: InputMaybe<StringOperationFilterInput>;
  county?: InputMaybe<StringOperationFilterInput>;
  creditLimitOnSalesOrder?: InputMaybe<ByteOperationFilterInput>;
  cstNo?: InputMaybe<StringOperationFilterInput>;
  customerNos?: InputMaybe<StringOperationFilterInput>;
  dearnessAllowance?: InputMaybe<DecimalOperationFilterInput>;
  deliveryOrderNos?: InputMaybe<StringOperationFilterInput>;
  depotGroup?: InputMaybe<StringOperationFilterInput>;
  discToDealerGLEco?: InputMaybe<StringOperationFilterInput>;
  discToDealerGLNo?: InputMaybe<StringOperationFilterInput>;
  eMail?: InputMaybe<StringOperationFilterInput>;
  einvPassword?: InputMaybe<StringOperationFilterInput>;
  einvUsername?: InputMaybe<StringOperationFilterInput>;
  employeeNos?: InputMaybe<StringOperationFilterInput>;
  esicEnabled?: InputMaybe<ByteOperationFilterInput>;
  esicRoundup?: InputMaybe<ByteOperationFilterInput>;
  ewaybillEnabled?: InputMaybe<ByteOperationFilterInput>;
  ewaybillFromDate?: InputMaybe<DateTimeOperationFilterInput>;
  exGratia?: InputMaybe<DecimalOperationFilterInput>;
  exciseCommissionerate?: InputMaybe<StringOperationFilterInput>;
  exciseDivision?: InputMaybe<StringOperationFilterInput>;
  exciseRange?: InputMaybe<StringOperationFilterInput>;
  exciseRegNo?: InputMaybe<StringOperationFilterInput>;
  expenseGLNo?: InputMaybe<StringOperationFilterInput>;
  faxNo?: InputMaybe<StringOperationFilterInput>;
  freightGLNo?: InputMaybe<StringOperationFilterInput>;
  getUnpostedEmpAttendance?: InputMaybe<ByteOperationFilterInput>;
  glForTDSOnTurnoverDisc?: InputMaybe<StringOperationFilterInput>;
  glForTurnoverDisc?: InputMaybe<StringOperationFilterInput>;
  globalDimension1Code?: InputMaybe<StringOperationFilterInput>;
  globalDimension2Code?: InputMaybe<StringOperationFilterInput>;
  gstEffectingDate?: InputMaybe<DateTimeOperationFilterInput>;
  gstGroupDef?: InputMaybe<StringOperationFilterInput>;
  gstInvoiceText?: InputMaybe<StringOperationFilterInput>;
  gstNo?: InputMaybe<StringOperationFilterInput>;
  gstTradeName?: InputMaybe<StringOperationFilterInput>;
  homePage?: InputMaybe<StringOperationFilterInput>;
  hreMailAddress?: InputMaybe<StringOperationFilterInput>;
  hsnsacNoDef?: InputMaybe<StringOperationFilterInput>;
  incentiveEarnCode?: InputMaybe<StringOperationFilterInput>;
  inspectionRequire?: InputMaybe<ByteOperationFilterInput>;
  jobWorkSheetNos?: InputMaybe<StringOperationFilterInput>;
  juridiction?: InputMaybe<StringOperationFilterInput>;
  lastEcomileCrnSync?: InputMaybe<StringOperationFilterInput>;
  lastEcomileInvSync?: InputMaybe<StringOperationFilterInput>;
  lastEcomilePcrnSync?: InputMaybe<StringOperationFilterInput>;
  lastEcomilePinvSync?: InputMaybe<StringOperationFilterInput>;
  leaveEarningMonthly?: InputMaybe<ByteOperationFilterInput>;
  licDedCode?: InputMaybe<StringOperationFilterInput>;
  locationCode?: InputMaybe<StringOperationFilterInput>;
  logo?: InputMaybe<ListByteOperationFilterInput>;
  minWages?: InputMaybe<DecimalOperationFilterInput>;
  miscSalesInvoiceNos?: InputMaybe<StringOperationFilterInput>;
  monthDaysInBonusCalc?: InputMaybe<ByteOperationFilterInput>;
  msmeNo?: InputMaybe<StringOperationFilterInput>;
  name?: InputMaybe<StringOperationFilterInput>;
  name2?: InputMaybe<StringOperationFilterInput>;
  natureOfBusiness?: InputMaybe<IntOperationFilterInput>;
  navConfig?: InputMaybe<StringOperationFilterInput>;
  npsDedCode?: InputMaybe<StringOperationFilterInput>;
  onlineOrderNos?: InputMaybe<StringOperationFilterInput>;
  onlyRepairs?: InputMaybe<DecimalOperationFilterInput>;
  onlyRepairsDisc?: InputMaybe<DecimalOperationFilterInput>;
  or?: InputMaybe<Array<ResponsibilityCenterFilterInput>>;
  otEarnCode?: InputMaybe<StringOperationFilterInput>;
  panNo?: InputMaybe<StringOperationFilterInput>;
  panOnSales?: InputMaybe<ByteOperationFilterInput>;
  partnerCode?: InputMaybe<StringOperationFilterInput>;
  partyNos?: InputMaybe<StringOperationFilterInput>;
  payroll?: InputMaybe<ByteOperationFilterInput>;
  pettyCashGLAccount?: InputMaybe<StringOperationFilterInput>;
  pfCalcSkipConditional?: InputMaybe<ByteOperationFilterInput>;
  pfEstCode?: InputMaybe<StringOperationFilterInput>;
  phoneNo?: InputMaybe<StringOperationFilterInput>;
  postCode?: InputMaybe<StringOperationFilterInput>;
  postDiscountToDealer?: InputMaybe<ByteOperationFilterInput>;
  postedAssemblyOrderNos?: InputMaybe<StringOperationFilterInput>;
  postedClaimOrderNos?: InputMaybe<StringOperationFilterInput>;
  postedDeliveryOrderNos?: InputMaybe<StringOperationFilterInput>;
  postedTFMOrderNos?: InputMaybe<StringOperationFilterInput>;
  priceReqOrderNos?: InputMaybe<StringOperationFilterInput>;
  procShipNos?: InputMaybe<StringOperationFilterInput>;
  procurementNos?: InputMaybe<StringOperationFilterInput>;
  prodIncAttendanceLimit?: InputMaybe<DecimalOperationFilterInput>;
  prodIncTotalUnits?: InputMaybe<DecimalOperationFilterInput>;
  prodIncentive?: InputMaybe<ByteOperationFilterInput>;
  production?: InputMaybe<ByteOperationFilterInput>;
  profTax?: InputMaybe<ByteOperationFilterInput>;
  purCrMemoNos?: InputMaybe<StringOperationFilterInput>;
  purCrMemoPostNos?: InputMaybe<StringOperationFilterInput>;
  purInvoiceNos?: InputMaybe<StringOperationFilterInput>;
  purInvoicePostNos?: InputMaybe<StringOperationFilterInput>;
  purOrderNos?: InputMaybe<StringOperationFilterInput>;
  purRecieptsNos?: InputMaybe<StringOperationFilterInput>;
  purchase?: InputMaybe<ByteOperationFilterInput>;
  regOfficeCode?: InputMaybe<StringOperationFilterInput>;
  respCenterSales?: InputMaybe<StringOperationFilterInput>;
  sale?: InputMaybe<ByteOperationFilterInput>;
  salesAmtLimitForPAN?: InputMaybe<DecimalOperationFilterInput>;
  salesClosureEndDate?: InputMaybe<DateTimeOperationFilterInput>;
  salesCrMemoNos?: InputMaybe<StringOperationFilterInput>;
  salesCrMemoPostNos?: InputMaybe<StringOperationFilterInput>;
  salesDrMemoNos?: InputMaybe<StringOperationFilterInput>;
  salesDrMemoPostNos?: InputMaybe<StringOperationFilterInput>;
  salesInvNosEcomile?: InputMaybe<StringOperationFilterInput>;
  salesInvoiceNos?: InputMaybe<StringOperationFilterInput>;
  salesInvoicePostNos?: InputMaybe<StringOperationFilterInput>;
  salesOrderNos?: InputMaybe<StringOperationFilterInput>;
  salesQuoteNos?: InputMaybe<StringOperationFilterInput>;
  salesShipmentNos?: InputMaybe<StringOperationFilterInput>;
  serviceTaxNo?: InputMaybe<StringOperationFilterInput>;
  shopCalendarCode?: InputMaybe<StringOperationFilterInput>;
  singlesheetdataNos?: InputMaybe<StringOperationFilterInput>;
  soPriceLimitBase?: InputMaybe<DecimalOperationFilterInput>;
  soPriceLimitGen?: InputMaybe<DecimalOperationFilterInput>;
  socLoanBatchName?: InputMaybe<StringOperationFilterInput>;
  socLoanEMIDedCode?: InputMaybe<StringOperationFilterInput>;
  socLoanInt?: InputMaybe<DecimalOperationFilterInput>;
  socLoanIntDedCode?: InputMaybe<StringOperationFilterInput>;
  socShareDedCode?: InputMaybe<StringOperationFilterInput>;
  staffLoanEMIDedCode?: InputMaybe<StringOperationFilterInput>;
  staffLoanInt?: InputMaybe<DecimalOperationFilterInput>;
  staffLoanIntDedCode?: InputMaybe<StringOperationFilterInput>;
  state?: InputMaybe<StringOperationFilterInput>;
  stopRoundupSalary?: InputMaybe<ByteOperationFilterInput>;
  supportDocMustOnCustomer?: InputMaybe<ByteOperationFilterInput>;
  supportDocMustOnDealer?: InputMaybe<ByteOperationFilterInput>;
  supportDocMustOnGRA?: InputMaybe<ByteOperationFilterInput>;
  tcsOnPurch?: InputMaybe<DecimalOperationFilterInput>;
  tcsOnSaleGLNo?: InputMaybe<StringOperationFilterInput>;
  tcsOnSales?: InputMaybe<DecimalOperationFilterInput>;
  tdsDedCode?: InputMaybe<StringOperationFilterInput>;
  tdsOnDealerEarnGLNo?: InputMaybe<StringOperationFilterInput>;
  tdsOnEarnNos?: InputMaybe<StringOperationFilterInput>;
  tdsOnTurnoverDisc?: InputMaybe<DecimalOperationFilterInput>;
  tdsRoundingUp?: InputMaybe<ByteOperationFilterInput>;
  tdsThersholdLimit?: InputMaybe<DecimalOperationFilterInput>;
  tdsWithPAN?: InputMaybe<DecimalOperationFilterInput>;
  tdsWithoutPAN?: InputMaybe<DecimalOperationFilterInput>;
  tfmOrderNos?: InputMaybe<StringOperationFilterInput>;
  tinNo?: InputMaybe<StringOperationFilterInput>;
  uheMailAddress?: InputMaybe<StringOperationFilterInput>;
  unitName?: InputMaybe<StringOperationFilterInput>;
  urdPurCrMemoPostNos?: InputMaybe<StringOperationFilterInput>;
  urdPurInvoicePostNos?: InputMaybe<StringOperationFilterInput>;
  vendorNos?: InputMaybe<StringOperationFilterInput>;
  yearlyBonus?: InputMaybe<DecimalOperationFilterInput>;
};

export type ResponsibilityCenterSortInput = {
  addIncetiveInSalAdv?: InputMaybe<SortEnumType>;
  address?: InputMaybe<SortEnumType>;
  address2?: InputMaybe<SortEnumType>;
  advanceDedCode?: InputMaybe<SortEnumType>;
  assCheckProdGroupQty?: InputMaybe<SortEnumType>;
  assQtyLimit?: InputMaybe<SortEnumType>;
  assemblyOrderNos?: InputMaybe<SortEnumType>;
  attendanceBonus?: InputMaybe<SortEnumType>;
  bankAccountNo?: InputMaybe<SortEnumType>;
  bonus?: InputMaybe<SortEnumType>;
  cinNo?: InputMaybe<SortEnumType>;
  city?: InputMaybe<SortEnumType>;
  claimOrderNos?: InputMaybe<SortEnumType>;
  code?: InputMaybe<SortEnumType>;
  companyName?: InputMaybe<SortEnumType>;
  contact?: InputMaybe<SortEnumType>;
  contactNo1?: InputMaybe<SortEnumType>;
  contactNo2?: InputMaybe<SortEnumType>;
  countryRegionCode?: InputMaybe<SortEnumType>;
  county?: InputMaybe<SortEnumType>;
  creditLimitOnSalesOrder?: InputMaybe<SortEnumType>;
  cstNo?: InputMaybe<SortEnumType>;
  customerNos?: InputMaybe<SortEnumType>;
  dearnessAllowance?: InputMaybe<SortEnumType>;
  deliveryOrderNos?: InputMaybe<SortEnumType>;
  depotGroup?: InputMaybe<SortEnumType>;
  discToDealerGLEco?: InputMaybe<SortEnumType>;
  discToDealerGLNo?: InputMaybe<SortEnumType>;
  eMail?: InputMaybe<SortEnumType>;
  einvPassword?: InputMaybe<SortEnumType>;
  einvUsername?: InputMaybe<SortEnumType>;
  employeeNos?: InputMaybe<SortEnumType>;
  esicEnabled?: InputMaybe<SortEnumType>;
  esicRoundup?: InputMaybe<SortEnumType>;
  ewaybillEnabled?: InputMaybe<SortEnumType>;
  ewaybillFromDate?: InputMaybe<SortEnumType>;
  exGratia?: InputMaybe<SortEnumType>;
  exciseCommissionerate?: InputMaybe<SortEnumType>;
  exciseDivision?: InputMaybe<SortEnumType>;
  exciseRange?: InputMaybe<SortEnumType>;
  exciseRegNo?: InputMaybe<SortEnumType>;
  expenseGLNo?: InputMaybe<SortEnumType>;
  faxNo?: InputMaybe<SortEnumType>;
  freightGLNo?: InputMaybe<SortEnumType>;
  getUnpostedEmpAttendance?: InputMaybe<SortEnumType>;
  glForTDSOnTurnoverDisc?: InputMaybe<SortEnumType>;
  glForTurnoverDisc?: InputMaybe<SortEnumType>;
  globalDimension1Code?: InputMaybe<SortEnumType>;
  globalDimension2Code?: InputMaybe<SortEnumType>;
  gstEffectingDate?: InputMaybe<SortEnumType>;
  gstGroupDef?: InputMaybe<SortEnumType>;
  gstInvoiceText?: InputMaybe<SortEnumType>;
  gstNo?: InputMaybe<SortEnumType>;
  gstTradeName?: InputMaybe<SortEnumType>;
  homePage?: InputMaybe<SortEnumType>;
  hreMailAddress?: InputMaybe<SortEnumType>;
  hsnsacNoDef?: InputMaybe<SortEnumType>;
  incentiveEarnCode?: InputMaybe<SortEnumType>;
  inspectionRequire?: InputMaybe<SortEnumType>;
  jobWorkSheetNos?: InputMaybe<SortEnumType>;
  juridiction?: InputMaybe<SortEnumType>;
  lastEcomileCrnSync?: InputMaybe<SortEnumType>;
  lastEcomileInvSync?: InputMaybe<SortEnumType>;
  lastEcomilePcrnSync?: InputMaybe<SortEnumType>;
  lastEcomilePinvSync?: InputMaybe<SortEnumType>;
  leaveEarningMonthly?: InputMaybe<SortEnumType>;
  licDedCode?: InputMaybe<SortEnumType>;
  locationCode?: InputMaybe<SortEnumType>;
  minWages?: InputMaybe<SortEnumType>;
  miscSalesInvoiceNos?: InputMaybe<SortEnumType>;
  monthDaysInBonusCalc?: InputMaybe<SortEnumType>;
  msmeNo?: InputMaybe<SortEnumType>;
  name?: InputMaybe<SortEnumType>;
  name2?: InputMaybe<SortEnumType>;
  natureOfBusiness?: InputMaybe<SortEnumType>;
  navConfig?: InputMaybe<SortEnumType>;
  npsDedCode?: InputMaybe<SortEnumType>;
  onlineOrderNos?: InputMaybe<SortEnumType>;
  onlyRepairs?: InputMaybe<SortEnumType>;
  onlyRepairsDisc?: InputMaybe<SortEnumType>;
  otEarnCode?: InputMaybe<SortEnumType>;
  panNo?: InputMaybe<SortEnumType>;
  panOnSales?: InputMaybe<SortEnumType>;
  partnerCode?: InputMaybe<SortEnumType>;
  partyNos?: InputMaybe<SortEnumType>;
  payroll?: InputMaybe<SortEnumType>;
  pettyCashGLAccount?: InputMaybe<SortEnumType>;
  pfCalcSkipConditional?: InputMaybe<SortEnumType>;
  pfEstCode?: InputMaybe<SortEnumType>;
  phoneNo?: InputMaybe<SortEnumType>;
  postCode?: InputMaybe<SortEnumType>;
  postDiscountToDealer?: InputMaybe<SortEnumType>;
  postedAssemblyOrderNos?: InputMaybe<SortEnumType>;
  postedClaimOrderNos?: InputMaybe<SortEnumType>;
  postedDeliveryOrderNos?: InputMaybe<SortEnumType>;
  postedTFMOrderNos?: InputMaybe<SortEnumType>;
  priceReqOrderNos?: InputMaybe<SortEnumType>;
  procShipNos?: InputMaybe<SortEnumType>;
  procurementNos?: InputMaybe<SortEnumType>;
  prodIncAttendanceLimit?: InputMaybe<SortEnumType>;
  prodIncTotalUnits?: InputMaybe<SortEnumType>;
  prodIncentive?: InputMaybe<SortEnumType>;
  production?: InputMaybe<SortEnumType>;
  profTax?: InputMaybe<SortEnumType>;
  purCrMemoNos?: InputMaybe<SortEnumType>;
  purCrMemoPostNos?: InputMaybe<SortEnumType>;
  purInvoiceNos?: InputMaybe<SortEnumType>;
  purInvoicePostNos?: InputMaybe<SortEnumType>;
  purOrderNos?: InputMaybe<SortEnumType>;
  purRecieptsNos?: InputMaybe<SortEnumType>;
  purchase?: InputMaybe<SortEnumType>;
  regOfficeCode?: InputMaybe<SortEnumType>;
  respCenterSales?: InputMaybe<SortEnumType>;
  sale?: InputMaybe<SortEnumType>;
  salesAmtLimitForPAN?: InputMaybe<SortEnumType>;
  salesClosureEndDate?: InputMaybe<SortEnumType>;
  salesCrMemoNos?: InputMaybe<SortEnumType>;
  salesCrMemoPostNos?: InputMaybe<SortEnumType>;
  salesDrMemoNos?: InputMaybe<SortEnumType>;
  salesDrMemoPostNos?: InputMaybe<SortEnumType>;
  salesInvNosEcomile?: InputMaybe<SortEnumType>;
  salesInvoiceNos?: InputMaybe<SortEnumType>;
  salesInvoicePostNos?: InputMaybe<SortEnumType>;
  salesOrderNos?: InputMaybe<SortEnumType>;
  salesQuoteNos?: InputMaybe<SortEnumType>;
  salesShipmentNos?: InputMaybe<SortEnumType>;
  serviceTaxNo?: InputMaybe<SortEnumType>;
  shopCalendarCode?: InputMaybe<SortEnumType>;
  singlesheetdataNos?: InputMaybe<SortEnumType>;
  soPriceLimitBase?: InputMaybe<SortEnumType>;
  soPriceLimitGen?: InputMaybe<SortEnumType>;
  socLoanBatchName?: InputMaybe<SortEnumType>;
  socLoanEMIDedCode?: InputMaybe<SortEnumType>;
  socLoanInt?: InputMaybe<SortEnumType>;
  socLoanIntDedCode?: InputMaybe<SortEnumType>;
  socShareDedCode?: InputMaybe<SortEnumType>;
  staffLoanEMIDedCode?: InputMaybe<SortEnumType>;
  staffLoanInt?: InputMaybe<SortEnumType>;
  staffLoanIntDedCode?: InputMaybe<SortEnumType>;
  state?: InputMaybe<SortEnumType>;
  stopRoundupSalary?: InputMaybe<SortEnumType>;
  supportDocMustOnCustomer?: InputMaybe<SortEnumType>;
  supportDocMustOnDealer?: InputMaybe<SortEnumType>;
  supportDocMustOnGRA?: InputMaybe<SortEnumType>;
  tcsOnPurch?: InputMaybe<SortEnumType>;
  tcsOnSaleGLNo?: InputMaybe<SortEnumType>;
  tcsOnSales?: InputMaybe<SortEnumType>;
  tdsDedCode?: InputMaybe<SortEnumType>;
  tdsOnDealerEarnGLNo?: InputMaybe<SortEnumType>;
  tdsOnEarnNos?: InputMaybe<SortEnumType>;
  tdsOnTurnoverDisc?: InputMaybe<SortEnumType>;
  tdsRoundingUp?: InputMaybe<SortEnumType>;
  tdsThersholdLimit?: InputMaybe<SortEnumType>;
  tdsWithPAN?: InputMaybe<SortEnumType>;
  tdsWithoutPAN?: InputMaybe<SortEnumType>;
  tfmOrderNos?: InputMaybe<SortEnumType>;
  tinNo?: InputMaybe<SortEnumType>;
  uheMailAddress?: InputMaybe<SortEnumType>;
  unitName?: InputMaybe<SortEnumType>;
  urdPurCrMemoPostNos?: InputMaybe<SortEnumType>;
  urdPurInvoicePostNos?: InputMaybe<SortEnumType>;
  vendorNos?: InputMaybe<SortEnumType>;
  yearlyBonus?: InputMaybe<SortEnumType>;
};

export type SalesReportParamsInput = {
  areas?: InputMaybe<Array<Scalars['String']['input']>>;
  /** Comma-separated customer numbers */
  customers?: InputMaybe<Scalars['String']['input']>;
  dealers?: InputMaybe<Array<Scalars['String']['input']>>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  entityDepartment?: InputMaybe<Scalars['String']['input']>;
  entityType?: InputMaybe<Scalars['String']['input']>;
  from?: InputMaybe<Scalars['String']['input']>;
  nos?: InputMaybe<Array<Scalars['String']['input']>>;
  regions?: InputMaybe<Array<Scalars['String']['input']>>;
  reportName?: InputMaybe<Scalars['String']['input']>;
  reportOutput?: InputMaybe<Scalars['String']['input']>;
  respCenters?: InputMaybe<Array<Scalars['String']['input']>>;
  search?: InputMaybe<Scalars['String']['input']>;
  skip?: InputMaybe<Scalars['Int']['input']>;
  take?: InputMaybe<Scalars['Int']['input']>;
  to?: InputMaybe<Scalars['String']['input']>;
  type?: InputMaybe<Scalars['String']['input']>;
  view?: InputMaybe<Scalars['String']['input']>;
  workDate?: InputMaybe<Scalars['String']['input']>;
};

export type SalespersonPurchaser = {
  __typename?: 'SalespersonPurchaser';
  aadharNo?: Maybe<Scalars['String']['output']>;
  bankACNo?: Maybe<Scalars['String']['output']>;
  bankBranch?: Maybe<Scalars['String']['output']>;
  bankIFSC?: Maybe<Scalars['String']['output']>;
  bankName?: Maybe<Scalars['String']['output']>;
  basePriceMaster?: Maybe<Scalars['String']['output']>;
  brandedShop: Scalars['Int']['output'];
  businessModel: Scalars['Int']['output'];
  code?: Maybe<Scalars['String']['output']>;
  commission: Scalars['Decimal']['output'];
  dateOfAniversary?: Maybe<Scalars['DateTime']['output']>;
  dateOfBirth?: Maybe<Scalars['DateTime']['output']>;
  dealershipExpDate?: Maybe<Scalars['DateTime']['output']>;
  dealershipName?: Maybe<Scalars['String']['output']>;
  dealershipStartDate?: Maybe<Scalars['DateTime']['output']>;
  depot?: Maybe<Scalars['String']['output']>;
  direct: Scalars['Byte']['output'];
  eMail?: Maybe<Scalars['String']['output']>;
  eMail2?: Maybe<Scalars['String']['output']>;
  globalDimension1Code?: Maybe<Scalars['String']['output']>;
  globalDimension2Code?: Maybe<Scalars['String']['output']>;
  group?: Maybe<Scalars['String']['output']>;
  gstNo?: Maybe<Scalars['String']['output']>;
  investmentAmount: Scalars['Decimal']['output'];
  mobileNo?: Maybe<Scalars['String']['output']>;
  name?: Maybe<Scalars['String']['output']>;
  noCommission: Scalars['Byte']['output'];
  panNo?: Maybe<Scalars['String']['output']>;
  primaryCustomerNo?: Maybe<Scalars['String']['output']>;
  product: Scalars['Int']['output'];
  responsibilityCenter?: Maybe<Scalars['String']['output']>;
  searchEMail?: Maybe<Scalars['String']['output']>;
  status: Scalars['Int']['output'];
};

export type SalespersonPurchaserFilterInput = {
  aadharNo?: InputMaybe<StringOperationFilterInput>;
  and?: InputMaybe<Array<SalespersonPurchaserFilterInput>>;
  bankACNo?: InputMaybe<StringOperationFilterInput>;
  bankBranch?: InputMaybe<StringOperationFilterInput>;
  bankIFSC?: InputMaybe<StringOperationFilterInput>;
  bankName?: InputMaybe<StringOperationFilterInput>;
  basePriceMaster?: InputMaybe<StringOperationFilterInput>;
  brandedShop?: InputMaybe<IntOperationFilterInput>;
  businessModel?: InputMaybe<IntOperationFilterInput>;
  code?: InputMaybe<StringOperationFilterInput>;
  commission?: InputMaybe<DecimalOperationFilterInput>;
  dateOfAniversary?: InputMaybe<DateTimeOperationFilterInput>;
  dateOfBirth?: InputMaybe<DateTimeOperationFilterInput>;
  dealershipExpDate?: InputMaybe<DateTimeOperationFilterInput>;
  dealershipName?: InputMaybe<StringOperationFilterInput>;
  dealershipStartDate?: InputMaybe<DateTimeOperationFilterInput>;
  depot?: InputMaybe<StringOperationFilterInput>;
  direct?: InputMaybe<ByteOperationFilterInput>;
  eMail?: InputMaybe<StringOperationFilterInput>;
  eMail2?: InputMaybe<StringOperationFilterInput>;
  globalDimension1Code?: InputMaybe<StringOperationFilterInput>;
  globalDimension2Code?: InputMaybe<StringOperationFilterInput>;
  group?: InputMaybe<StringOperationFilterInput>;
  gstNo?: InputMaybe<StringOperationFilterInput>;
  investmentAmount?: InputMaybe<DecimalOperationFilterInput>;
  mobileNo?: InputMaybe<StringOperationFilterInput>;
  name?: InputMaybe<StringOperationFilterInput>;
  noCommission?: InputMaybe<ByteOperationFilterInput>;
  or?: InputMaybe<Array<SalespersonPurchaserFilterInput>>;
  panNo?: InputMaybe<StringOperationFilterInput>;
  primaryCustomerNo?: InputMaybe<StringOperationFilterInput>;
  product?: InputMaybe<IntOperationFilterInput>;
  responsibilityCenter?: InputMaybe<StringOperationFilterInput>;
  searchEMail?: InputMaybe<StringOperationFilterInput>;
  status?: InputMaybe<IntOperationFilterInput>;
};

export type SalespersonPurchaserSortInput = {
  aadharNo?: InputMaybe<SortEnumType>;
  bankACNo?: InputMaybe<SortEnumType>;
  bankBranch?: InputMaybe<SortEnumType>;
  bankIFSC?: InputMaybe<SortEnumType>;
  bankName?: InputMaybe<SortEnumType>;
  basePriceMaster?: InputMaybe<SortEnumType>;
  brandedShop?: InputMaybe<SortEnumType>;
  businessModel?: InputMaybe<SortEnumType>;
  code?: InputMaybe<SortEnumType>;
  commission?: InputMaybe<SortEnumType>;
  dateOfAniversary?: InputMaybe<SortEnumType>;
  dateOfBirth?: InputMaybe<SortEnumType>;
  dealershipExpDate?: InputMaybe<SortEnumType>;
  dealershipName?: InputMaybe<SortEnumType>;
  dealershipStartDate?: InputMaybe<SortEnumType>;
  depot?: InputMaybe<SortEnumType>;
  direct?: InputMaybe<SortEnumType>;
  eMail?: InputMaybe<SortEnumType>;
  eMail2?: InputMaybe<SortEnumType>;
  globalDimension1Code?: InputMaybe<SortEnumType>;
  globalDimension2Code?: InputMaybe<SortEnumType>;
  group?: InputMaybe<SortEnumType>;
  gstNo?: InputMaybe<SortEnumType>;
  investmentAmount?: InputMaybe<SortEnumType>;
  mobileNo?: InputMaybe<SortEnumType>;
  name?: InputMaybe<SortEnumType>;
  noCommission?: InputMaybe<SortEnumType>;
  panNo?: InputMaybe<SortEnumType>;
  primaryCustomerNo?: InputMaybe<SortEnumType>;
  product?: InputMaybe<SortEnumType>;
  responsibilityCenter?: InputMaybe<SortEnumType>;
  searchEMail?: InputMaybe<SortEnumType>;
  status?: InputMaybe<SortEnumType>;
};

export type SaveDealerInput = {
  aadharNo: Scalars['String']['input'];
  bankACNo: Scalars['String']['input'];
  bankBranch: Scalars['String']['input'];
  bankIFSC: Scalars['String']['input'];
  bankName: Scalars['String']['input'];
  brandedShop: Scalars['Int']['input'];
  businessModel: Scalars['Int']['input'];
  code: Scalars['String']['input'];
  dateOfAniversary: Scalars['DateTime']['input'];
  dateOfBirth: Scalars['DateTime']['input'];
  dealershipExpDate: Scalars['DateTime']['input'];
  dealershipName: Scalars['String']['input'];
  dealershipStartDate: Scalars['DateTime']['input'];
  eMail: Scalars['String']['input'];
  gstNo: Scalars['String']['input'];
  investmentAmount: Scalars['Decimal']['input'];
  mobileNo: Scalars['String']['input'];
  name: Scalars['String']['input'];
  panNo: Scalars['String']['input'];
  product: Scalars['Int']['input'];
  status: Scalars['Int']['input'];
};

export type SellerDetail = {
  __typename?: 'SellerDetail';
  addr1: Scalars['String']['output'];
  addr2?: Maybe<Scalars['String']['output']>;
  em?: Maybe<Scalars['String']['output']>;
  gstin: Scalars['String']['output'];
  lglNm: Scalars['String']['output'];
  loc: Scalars['String']['output'];
  ph?: Maybe<Scalars['String']['output']>;
  pin: Scalars['Int']['output'];
  stcd: Scalars['String']['output'];
  trdNm?: Maybe<Scalars['String']['output']>;
};

export type SessionInfo = {
  __typename?: 'SessionInfo';
  createdAtUtc: Scalars['DateTime']['output'];
  department: Scalars['String']['output'];
  entityCode: Scalars['String']['output'];
  entityType: Scalars['String']['output'];
  expiresAtUtc: Scalars['DateTime']['output'];
  sessionId: Scalars['String']['output'];
  userId: Scalars['String']['output'];
  userSecurityId: Scalars['UUID']['output'];
  userType: Scalars['String']['output'];
};

export type SetProfileResult = {
  __typename?: 'SetProfileResult';
  message: Scalars['String']['output'];
  success: Scalars['Boolean']['output'];
};

export type ShipDetail = {
  __typename?: 'ShipDetail';
  addr1: Scalars['String']['output'];
  addr2?: Maybe<Scalars['String']['output']>;
  gstin?: Maybe<Scalars['String']['output']>;
  lglNm: Scalars['String']['output'];
  loc?: Maybe<Scalars['String']['output']>;
  pin: Scalars['Int']['output'];
  stcd: Scalars['String']['output'];
  trdNm?: Maybe<Scalars['String']['output']>;
};

export type ShipmentInfo = {
  __typename?: 'ShipmentInfo';
  date: Scalars['String']['output'];
  destination: Scalars['String']['output'];
  mobileNo: Scalars['String']['output'];
  no: Scalars['String']['output'];
  transport: Scalars['String']['output'];
  vehicleNo: Scalars['String']['output'];
};

export enum SortEnumType {
  Asc = 'ASC',
  Desc = 'DESC'
}

export type StringOperationFilterInput = {
  and?: InputMaybe<Array<StringOperationFilterInput>>;
  contains?: InputMaybe<Scalars['String']['input']>;
  endsWith?: InputMaybe<Scalars['String']['input']>;
  eq?: InputMaybe<Scalars['String']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['String']['input']>>>;
  ncontains?: InputMaybe<Scalars['String']['input']>;
  nendsWith?: InputMaybe<Scalars['String']['input']>;
  neq?: InputMaybe<Scalars['String']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['String']['input']>>>;
  nstartsWith?: InputMaybe<Scalars['String']['input']>;
  or?: InputMaybe<Array<StringOperationFilterInput>>;
  startsWith?: InputMaybe<Scalars['String']['input']>;
};

export type SubMenu = {
  __typename?: 'SubMenu';
  icon: Scalars['String']['output'];
  items: Array<MenuItem>;
  label: Scalars['String']['output'];
};

export type Subscription = {
  __typename?: 'Subscription';
  onNotification: Notification;
};


export type SubscriptionOnNotificationArgs = {
  userId: Scalars['String']['input'];
};

export type Territory = {
  __typename?: 'Territory';
  bankAcNo?: Maybe<Scalars['String']['output']>;
  bankAcNo2?: Maybe<Scalars['String']['output']>;
  code?: Maybe<Scalars['String']['output']>;
  dealercodePrefix?: Maybe<Scalars['String']['output']>;
  emailAddress?: Maybe<Scalars['String']['output']>;
  genBusPostingGroup?: Maybe<Scalars['String']['output']>;
  name?: Maybe<Scalars['String']['output']>;
  responsibilityCenter?: Maybe<Scalars['String']['output']>;
  scrDealerCode?: Maybe<Scalars['String']['output']>;
  type: Scalars['Int']['output'];
};

export type TerritoryFilterInput = {
  and?: InputMaybe<Array<TerritoryFilterInput>>;
  bankAcNo?: InputMaybe<StringOperationFilterInput>;
  bankAcNo2?: InputMaybe<StringOperationFilterInput>;
  code?: InputMaybe<StringOperationFilterInput>;
  dealercodePrefix?: InputMaybe<StringOperationFilterInput>;
  emailAddress?: InputMaybe<StringOperationFilterInput>;
  genBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  name?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<TerritoryFilterInput>>;
  responsibilityCenter?: InputMaybe<StringOperationFilterInput>;
  scrDealerCode?: InputMaybe<StringOperationFilterInput>;
  type?: InputMaybe<IntOperationFilterInput>;
};

export type TerritorySortInput = {
  bankAcNo?: InputMaybe<SortEnumType>;
  bankAcNo2?: InputMaybe<SortEnumType>;
  code?: InputMaybe<SortEnumType>;
  dealercodePrefix?: InputMaybe<SortEnumType>;
  emailAddress?: InputMaybe<SortEnumType>;
  genBusPostingGroup?: InputMaybe<SortEnumType>;
  name?: InputMaybe<SortEnumType>;
  responsibilityCenter?: InputMaybe<SortEnumType>;
  scrDealerCode?: InputMaybe<SortEnumType>;
  type?: InputMaybe<SortEnumType>;
};

export type Tile = {
  __typename?: 'Tile';
  description: Scalars['String']['output'];
  label: Scalars['String']['output'];
  value: Scalars['Decimal']['output'];
};

export type TransDetail = {
  __typename?: 'TransDetail';
  ecmGstin?: Maybe<Scalars['String']['output']>;
  igstOnIntra: Scalars['String']['output'];
  regRev: Scalars['String']['output'];
  supTyp: Scalars['String']['output'];
  taxSch: Scalars['String']['output'];
};

export type UpdateEventInput = {
  attendees?: InputMaybe<Array<AttendeeInput>>;
  description?: InputMaybe<Scalars['String']['input']>;
  endUtc?: InputMaybe<Scalars['DateTime']['input']>;
  eventTypeId?: InputMaybe<Scalars['Int']['input']>;
  isAllDay?: InputMaybe<Scalars['Boolean']['input']>;
  location?: InputMaybe<Scalars['String']['input']>;
  meetingLink?: InputMaybe<Scalars['String']['input']>;
  recurrence?: InputMaybe<RecurrenceInput>;
  reminders?: InputMaybe<Array<ReminderInput>>;
  showAs?: InputMaybe<Scalars['Int']['input']>;
  startUtc?: InputMaybe<Scalars['DateTime']['input']>;
  status?: InputMaybe<Scalars['Int']['input']>;
  tags?: InputMaybe<Array<EventTagInput>>;
  tasks?: InputMaybe<Array<CalendarTaskInput>>;
  timeZoneId?: InputMaybe<Scalars['String']['input']>;
  title?: InputMaybe<Scalars['String']['input']>;
  visibility?: InputMaybe<Scalars['Int']['input']>;
};

export type UserDetail = {
  __typename?: 'UserDetail';
  fullName: Scalars['String']['output'];
  navConfigName?: Maybe<Scalars['String']['output']>;
  rdpPassword?: Maybe<Scalars['String']['output']>;
  userId: Scalars['String']['output'];
};

export type UserEntity = {
  __typename?: 'UserEntity';
  code: Scalars['String']['output'];
  location: Scalars['String']['output'];
  name: Scalars['String']['output'];
  title: Scalars['String']['output'];
};

export type UserLocation = {
  __typename?: 'UserLocation';
  code: Scalars['String']['output'];
  name: Scalars['String']['output'];
  payroll: Scalars['Byte']['output'];
  production: Scalars['Byte']['output'];
  purchase: Scalars['Byte']['output'];
  sale: Scalars['Byte']['output'];
};

export type UserSearchResult = {
  __typename?: 'UserSearchResult';
  avatar?: Maybe<Scalars['Int']['output']>;
  fullName: Scalars['String']['output'];
  userId: Scalars['String']['output'];
  userType: Scalars['String']['output'];
};

export type UuidOperationFilterInput = {
  eq?: InputMaybe<Scalars['UUID']['input']>;
  gt?: InputMaybe<Scalars['UUID']['input']>;
  gte?: InputMaybe<Scalars['UUID']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['UUID']['input']>>>;
  lt?: InputMaybe<Scalars['UUID']['input']>;
  lte?: InputMaybe<Scalars['UUID']['input']>;
  neq?: InputMaybe<Scalars['UUID']['input']>;
  ngt?: InputMaybe<Scalars['UUID']['input']>;
  ngte?: InputMaybe<Scalars['UUID']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['UUID']['input']>>>;
  nlt?: InputMaybe<Scalars['UUID']['input']>;
  nlte?: InputMaybe<Scalars['UUID']['input']>;
};

export type ValueDetails = {
  __typename?: 'ValueDetails';
  assVal: Scalars['Decimal']['output'];
  cesVal: Scalars['Decimal']['output'];
  cgstVal: Scalars['Decimal']['output'];
  discount: Scalars['Decimal']['output'];
  igstVal: Scalars['Decimal']['output'];
  othChrg: Scalars['Decimal']['output'];
  rndOffAmt: Scalars['Decimal']['output'];
  sgstVal: Scalars['Decimal']['output'];
  stCesVal: Scalars['Decimal']['output'];
  totInvVal: Scalars['Decimal']['output'];
};

export type Vehicles = {
  __typename?: 'Vehicles';
  gstNo?: Maybe<Scalars['String']['output']>;
  lineNo: Scalars['Int']['output'];
  mobileNo?: Maybe<Scalars['String']['output']>;
  name?: Maybe<Scalars['String']['output']>;
  no?: Maybe<Scalars['String']['output']>;
  responsibilityCenter?: Maybe<Scalars['String']['output']>;
  status: Scalars['Int']['output'];
};

export type VehiclesFilterInput = {
  and?: InputMaybe<Array<VehiclesFilterInput>>;
  gstNo?: InputMaybe<StringOperationFilterInput>;
  lineNo?: InputMaybe<IntOperationFilterInput>;
  mobileNo?: InputMaybe<StringOperationFilterInput>;
  name?: InputMaybe<StringOperationFilterInput>;
  no?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<VehiclesFilterInput>>;
  responsibilityCenter?: InputMaybe<StringOperationFilterInput>;
  status?: InputMaybe<IntOperationFilterInput>;
};

export type VehiclesSortInput = {
  gstNo?: InputMaybe<SortEnumType>;
  lineNo?: InputMaybe<SortEnumType>;
  mobileNo?: InputMaybe<SortEnumType>;
  name?: InputMaybe<SortEnumType>;
  no?: InputMaybe<SortEnumType>;
  responsibilityCenter?: InputMaybe<SortEnumType>;
  status?: InputMaybe<SortEnumType>;
};

export type Vendor = {
  __typename?: 'Vendor';
  address?: Maybe<Scalars['String']['output']>;
  address2?: Maybe<Scalars['String']['output']>;
  adhaarNo?: Maybe<Scalars['String']['output']>;
  applicationMethod: Scalars['Int']['output'];
  associatedEnterprises: Scalars['Byte']['output'];
  balance?: Maybe<Scalars['Decimal']['output']>;
  bankACNo?: Maybe<Scalars['String']['output']>;
  bankBranch?: Maybe<Scalars['String']['output']>;
  bankIFSCCode?: Maybe<Scalars['String']['output']>;
  bankName?: Maybe<Scalars['String']['output']>;
  baseCalendarCode?: Maybe<Scalars['String']['output']>;
  blockPaymentTolerance: Scalars['Byte']['output'];
  blocked: Scalars['Int']['output'];
  budgetedAmount: Scalars['Decimal']['output'];
  cashFlowPaymentTermsCode?: Maybe<Scalars['String']['output']>;
  city?: Maybe<Scalars['String']['output']>;
  collectorate?: Maybe<Scalars['String']['output']>;
  commissionerSPermissionNo?: Maybe<Scalars['String']['output']>;
  composition: Scalars['Byte']['output'];
  contact?: Maybe<Scalars['String']['output']>;
  countryRegionCode?: Maybe<Scalars['String']['output']>;
  county?: Maybe<Scalars['String']['output']>;
  creatorUserID?: Maybe<Scalars['String']['output']>;
  creditorNo?: Maybe<Scalars['String']['output']>;
  cstNo?: Maybe<Scalars['String']['output']>;
  currencyCode?: Maybe<Scalars['String']['output']>;
  eMail?: Maybe<Scalars['String']['output']>;
  eccNo?: Maybe<Scalars['String']['output']>;
  ecomileProcMgr?: Maybe<Scalars['String']['output']>;
  exciseBusPostingGroup?: Maybe<Scalars['String']['output']>;
  faxNo?: Maybe<Scalars['String']['output']>;
  finChargeTermsCode?: Maybe<Scalars['String']['output']>;
  genBusPostingGroup?: Maybe<Scalars['String']['output']>;
  globalDimension1Code?: Maybe<Scalars['String']['output']>;
  globalDimension2Code?: Maybe<Scalars['String']['output']>;
  groupCategory?: Maybe<Scalars['String']['output']>;
  groupDetails?: Maybe<Scalars['String']['output']>;
  gstBlockStatus: Scalars['Int']['output'];
  gstLegalName?: Maybe<Scalars['String']['output']>;
  gstRegistrationNo?: Maybe<Scalars['String']['output']>;
  gstStatus: Scalars['Int']['output'];
  gstStatusCheck?: Maybe<Scalars['DateTime']['output']>;
  gstSupplyType: Scalars['Int']['output'];
  gstTradeName?: Maybe<Scalars['String']['output']>;
  gstVendorType: Scalars['Int']['output'];
  homePage?: Maybe<Scalars['String']['output']>;
  icPartnerCode?: Maybe<Scalars['String']['output']>;
  invoiceDiscCode?: Maybe<Scalars['String']['output']>;
  languageCode?: Maybe<Scalars['String']['output']>;
  lastDateModified?: Maybe<Scalars['DateTime']['output']>;
  leadTimeCalculation?: Maybe<Scalars['String']['output']>;
  locationCode?: Maybe<Scalars['String']['output']>;
  lstNo?: Maybe<Scalars['String']['output']>;
  msmeRegNo?: Maybe<Scalars['String']['output']>;
  name?: Maybe<Scalars['String']['output']>;
  name2?: Maybe<Scalars['String']['output']>;
  nameOnInvoice?: Maybe<Scalars['String']['output']>;
  no?: Maybe<Scalars['String']['output']>;
  noSeries?: Maybe<Scalars['String']['output']>;
  ourAccountNo?: Maybe<Scalars['String']['output']>;
  panNo?: Maybe<Scalars['String']['output']>;
  panReferenceNo?: Maybe<Scalars['String']['output']>;
  panStatus: Scalars['Int']['output'];
  partnerType: Scalars['Int']['output'];
  payToVendorNo?: Maybe<Scalars['String']['output']>;
  paymentMethodCode?: Maybe<Scalars['String']['output']>;
  paymentTermsCode?: Maybe<Scalars['String']['output']>;
  phoneNo?: Maybe<Scalars['String']['output']>;
  picture?: Maybe<Array<Scalars['Byte']['output']>>;
  postCode?: Maybe<Scalars['String']['output']>;
  preferredBankAccount?: Maybe<Scalars['String']['output']>;
  prepayment: Scalars['Decimal']['output'];
  pricesIncludingVAT: Scalars['Byte']['output'];
  primaryContactNo?: Maybe<Scalars['String']['output']>;
  priority: Scalars['Int']['output'];
  purchaserCode?: Maybe<Scalars['String']['output']>;
  range?: Maybe<Scalars['String']['output']>;
  responsibilityCenter?: Maybe<Scalars['String']['output']>;
  searchName?: Maybe<Scalars['String']['output']>;
  selfInvoice: Scalars['Byte']['output'];
  serviceEntityType?: Maybe<Scalars['String']['output']>;
  serviceTaxRegistrationNo?: Maybe<Scalars['String']['output']>;
  shipmentMethodCode?: Maybe<Scalars['String']['output']>;
  shippingAgentCode?: Maybe<Scalars['String']['output']>;
  ssi: Scalars['Byte']['output'];
  ssiValidityDate?: Maybe<Scalars['DateTime']['output']>;
  stateCode?: Maybe<Scalars['String']['output']>;
  statisticsGroup: Scalars['Int']['output'];
  stopAutoCorrectName: Scalars['Byte']['output'];
  structure?: Maybe<Scalars['String']['output']>;
  subcontractor: Scalars['Byte']['output'];
  taxAreaCode?: Maybe<Scalars['String']['output']>;
  taxLiable: Scalars['Byte']['output'];
  tcsOnPurchase: Scalars['Byte']['output'];
  telexAnswerBack?: Maybe<Scalars['String']['output']>;
  telexNo?: Maybe<Scalars['String']['output']>;
  territoryCode?: Maybe<Scalars['String']['output']>;
  tinNo?: Maybe<Scalars['String']['output']>;
  transporter: Scalars['Byte']['output'];
  vatBusPostingGroup?: Maybe<Scalars['String']['output']>;
  vatRegistrationNo?: Maybe<Scalars['String']['output']>;
  vendorLocation?: Maybe<Scalars['String']['output']>;
  vendorPostingGroup?: Maybe<Scalars['String']['output']>;
  vendorType: Scalars['Int']['output'];
};

export type VendorFilterInput = {
  address?: InputMaybe<StringOperationFilterInput>;
  address2?: InputMaybe<StringOperationFilterInput>;
  adhaarNo?: InputMaybe<StringOperationFilterInput>;
  and?: InputMaybe<Array<VendorFilterInput>>;
  applicationMethod?: InputMaybe<IntOperationFilterInput>;
  associatedEnterprises?: InputMaybe<ByteOperationFilterInput>;
  balance?: InputMaybe<DecimalOperationFilterInput>;
  bankACNo?: InputMaybe<StringOperationFilterInput>;
  bankBranch?: InputMaybe<StringOperationFilterInput>;
  bankIFSCCode?: InputMaybe<StringOperationFilterInput>;
  bankName?: InputMaybe<StringOperationFilterInput>;
  baseCalendarCode?: InputMaybe<StringOperationFilterInput>;
  blockPaymentTolerance?: InputMaybe<ByteOperationFilterInput>;
  blocked?: InputMaybe<IntOperationFilterInput>;
  budgetedAmount?: InputMaybe<DecimalOperationFilterInput>;
  cashFlowPaymentTermsCode?: InputMaybe<StringOperationFilterInput>;
  city?: InputMaybe<StringOperationFilterInput>;
  collectorate?: InputMaybe<StringOperationFilterInput>;
  commissionerSPermissionNo?: InputMaybe<StringOperationFilterInput>;
  composition?: InputMaybe<ByteOperationFilterInput>;
  contact?: InputMaybe<StringOperationFilterInput>;
  countryRegionCode?: InputMaybe<StringOperationFilterInput>;
  county?: InputMaybe<StringOperationFilterInput>;
  creatorUserID?: InputMaybe<StringOperationFilterInput>;
  creditorNo?: InputMaybe<StringOperationFilterInput>;
  cstNo?: InputMaybe<StringOperationFilterInput>;
  currencyCode?: InputMaybe<StringOperationFilterInput>;
  eMail?: InputMaybe<StringOperationFilterInput>;
  eccNo?: InputMaybe<StringOperationFilterInput>;
  ecomileProcMgr?: InputMaybe<StringOperationFilterInput>;
  exciseBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  faxNo?: InputMaybe<StringOperationFilterInput>;
  finChargeTermsCode?: InputMaybe<StringOperationFilterInput>;
  genBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  globalDimension1Code?: InputMaybe<StringOperationFilterInput>;
  globalDimension2Code?: InputMaybe<StringOperationFilterInput>;
  groupCategory?: InputMaybe<StringOperationFilterInput>;
  groupDetails?: InputMaybe<StringOperationFilterInput>;
  gstBlockStatus?: InputMaybe<IntOperationFilterInput>;
  gstLegalName?: InputMaybe<StringOperationFilterInput>;
  gstRegistrationNo?: InputMaybe<StringOperationFilterInput>;
  gstStatus?: InputMaybe<IntOperationFilterInput>;
  gstStatusCheck?: InputMaybe<DateTimeOperationFilterInput>;
  gstSupplyType?: InputMaybe<IntOperationFilterInput>;
  gstTradeName?: InputMaybe<StringOperationFilterInput>;
  gstVendorType?: InputMaybe<IntOperationFilterInput>;
  homePage?: InputMaybe<StringOperationFilterInput>;
  icPartnerCode?: InputMaybe<StringOperationFilterInput>;
  invoiceDiscCode?: InputMaybe<StringOperationFilterInput>;
  languageCode?: InputMaybe<StringOperationFilterInput>;
  lastDateModified?: InputMaybe<DateTimeOperationFilterInput>;
  leadTimeCalculation?: InputMaybe<StringOperationFilterInput>;
  locationCode?: InputMaybe<StringOperationFilterInput>;
  lstNo?: InputMaybe<StringOperationFilterInput>;
  msmeRegNo?: InputMaybe<StringOperationFilterInput>;
  name?: InputMaybe<StringOperationFilterInput>;
  name2?: InputMaybe<StringOperationFilterInput>;
  nameOnInvoice?: InputMaybe<StringOperationFilterInput>;
  no?: InputMaybe<StringOperationFilterInput>;
  noSeries?: InputMaybe<StringOperationFilterInput>;
  or?: InputMaybe<Array<VendorFilterInput>>;
  ourAccountNo?: InputMaybe<StringOperationFilterInput>;
  panNo?: InputMaybe<StringOperationFilterInput>;
  panReferenceNo?: InputMaybe<StringOperationFilterInput>;
  panStatus?: InputMaybe<IntOperationFilterInput>;
  partnerType?: InputMaybe<IntOperationFilterInput>;
  payToVendorNo?: InputMaybe<StringOperationFilterInput>;
  paymentMethodCode?: InputMaybe<StringOperationFilterInput>;
  paymentTermsCode?: InputMaybe<StringOperationFilterInput>;
  phoneNo?: InputMaybe<StringOperationFilterInput>;
  picture?: InputMaybe<ListByteOperationFilterInput>;
  postCode?: InputMaybe<StringOperationFilterInput>;
  preferredBankAccount?: InputMaybe<StringOperationFilterInput>;
  prepayment?: InputMaybe<DecimalOperationFilterInput>;
  pricesIncludingVAT?: InputMaybe<ByteOperationFilterInput>;
  primaryContactNo?: InputMaybe<StringOperationFilterInput>;
  priority?: InputMaybe<IntOperationFilterInput>;
  purchaserCode?: InputMaybe<StringOperationFilterInput>;
  range?: InputMaybe<StringOperationFilterInput>;
  responsibilityCenter?: InputMaybe<StringOperationFilterInput>;
  searchName?: InputMaybe<StringOperationFilterInput>;
  selfInvoice?: InputMaybe<ByteOperationFilterInput>;
  serviceEntityType?: InputMaybe<StringOperationFilterInput>;
  serviceTaxRegistrationNo?: InputMaybe<StringOperationFilterInput>;
  shipmentMethodCode?: InputMaybe<StringOperationFilterInput>;
  shippingAgentCode?: InputMaybe<StringOperationFilterInput>;
  ssi?: InputMaybe<ByteOperationFilterInput>;
  ssiValidityDate?: InputMaybe<DateTimeOperationFilterInput>;
  stateCode?: InputMaybe<StringOperationFilterInput>;
  statisticsGroup?: InputMaybe<IntOperationFilterInput>;
  stopAutoCorrectName?: InputMaybe<ByteOperationFilterInput>;
  structure?: InputMaybe<StringOperationFilterInput>;
  subcontractor?: InputMaybe<ByteOperationFilterInput>;
  taxAreaCode?: InputMaybe<StringOperationFilterInput>;
  taxLiable?: InputMaybe<ByteOperationFilterInput>;
  tcsOnPurchase?: InputMaybe<ByteOperationFilterInput>;
  telexAnswerBack?: InputMaybe<StringOperationFilterInput>;
  telexNo?: InputMaybe<StringOperationFilterInput>;
  territoryCode?: InputMaybe<StringOperationFilterInput>;
  tinNo?: InputMaybe<StringOperationFilterInput>;
  transporter?: InputMaybe<ByteOperationFilterInput>;
  vatBusPostingGroup?: InputMaybe<StringOperationFilterInput>;
  vatRegistrationNo?: InputMaybe<StringOperationFilterInput>;
  vendorLocation?: InputMaybe<StringOperationFilterInput>;
  vendorPostingGroup?: InputMaybe<StringOperationFilterInput>;
  vendorType?: InputMaybe<IntOperationFilterInput>;
};

export type VendorModel = {
  __typename?: 'VendorModel';
  address: Scalars['String']['output'];
  address2: Scalars['String']['output'];
  adhaarNo: Scalars['String']['output'];
  balance: Scalars['Decimal']['output'];
  bankAccNo: Scalars['String']['output'];
  bankBranch: Scalars['String']['output'];
  bankIFSC: Scalars['String']['output'];
  bankName: Scalars['String']['output'];
  category: Scalars['String']['output'];
  city: Scalars['String']['output'];
  detail: Scalars['String']['output'];
  ecoMgrCode: Scalars['String']['output'];
  mobileNo: Scalars['String']['output'];
  name: Scalars['String']['output'];
  nameOnInvoice: Scalars['String']['output'];
  no: Scalars['String']['output'];
  panNo: Scalars['String']['output'];
  postCode: Scalars['String']['output'];
  respCenter: Scalars['String']['output'];
  selfInvoice: Scalars['Boolean']['output'];
  stateCode: Scalars['String']['output'];
};

export type VendorModelInput = {
  address: Scalars['String']['input'];
  address2: Scalars['String']['input'];
  adhaarNo: Scalars['String']['input'];
  balance: Scalars['Decimal']['input'];
  bankAccNo: Scalars['String']['input'];
  bankBranch: Scalars['String']['input'];
  bankIFSC: Scalars['String']['input'];
  bankName: Scalars['String']['input'];
  category: Scalars['String']['input'];
  city: Scalars['String']['input'];
  detail: Scalars['String']['input'];
  ecoMgrCode: Scalars['String']['input'];
  mobileNo: Scalars['String']['input'];
  name: Scalars['String']['input'];
  nameOnInvoice: Scalars['String']['input'];
  no: Scalars['String']['input'];
  panNo: Scalars['String']['input'];
  postCode: Scalars['String']['input'];
  respCenter: Scalars['String']['input'];
  selfInvoice: Scalars['Boolean']['input'];
  stateCode: Scalars['String']['input'];
};

export type VendorSortInput = {
  address?: InputMaybe<SortEnumType>;
  address2?: InputMaybe<SortEnumType>;
  adhaarNo?: InputMaybe<SortEnumType>;
  applicationMethod?: InputMaybe<SortEnumType>;
  associatedEnterprises?: InputMaybe<SortEnumType>;
  balance?: InputMaybe<SortEnumType>;
  bankACNo?: InputMaybe<SortEnumType>;
  bankBranch?: InputMaybe<SortEnumType>;
  bankIFSCCode?: InputMaybe<SortEnumType>;
  bankName?: InputMaybe<SortEnumType>;
  baseCalendarCode?: InputMaybe<SortEnumType>;
  blockPaymentTolerance?: InputMaybe<SortEnumType>;
  blocked?: InputMaybe<SortEnumType>;
  budgetedAmount?: InputMaybe<SortEnumType>;
  cashFlowPaymentTermsCode?: InputMaybe<SortEnumType>;
  city?: InputMaybe<SortEnumType>;
  collectorate?: InputMaybe<SortEnumType>;
  commissionerSPermissionNo?: InputMaybe<SortEnumType>;
  composition?: InputMaybe<SortEnumType>;
  contact?: InputMaybe<SortEnumType>;
  countryRegionCode?: InputMaybe<SortEnumType>;
  county?: InputMaybe<SortEnumType>;
  creatorUserID?: InputMaybe<SortEnumType>;
  creditorNo?: InputMaybe<SortEnumType>;
  cstNo?: InputMaybe<SortEnumType>;
  currencyCode?: InputMaybe<SortEnumType>;
  eMail?: InputMaybe<SortEnumType>;
  eccNo?: InputMaybe<SortEnumType>;
  ecomileProcMgr?: InputMaybe<SortEnumType>;
  exciseBusPostingGroup?: InputMaybe<SortEnumType>;
  faxNo?: InputMaybe<SortEnumType>;
  finChargeTermsCode?: InputMaybe<SortEnumType>;
  genBusPostingGroup?: InputMaybe<SortEnumType>;
  globalDimension1Code?: InputMaybe<SortEnumType>;
  globalDimension2Code?: InputMaybe<SortEnumType>;
  groupCategory?: InputMaybe<SortEnumType>;
  groupDetails?: InputMaybe<SortEnumType>;
  gstBlockStatus?: InputMaybe<SortEnumType>;
  gstLegalName?: InputMaybe<SortEnumType>;
  gstRegistrationNo?: InputMaybe<SortEnumType>;
  gstStatus?: InputMaybe<SortEnumType>;
  gstStatusCheck?: InputMaybe<SortEnumType>;
  gstSupplyType?: InputMaybe<SortEnumType>;
  gstTradeName?: InputMaybe<SortEnumType>;
  gstVendorType?: InputMaybe<SortEnumType>;
  homePage?: InputMaybe<SortEnumType>;
  icPartnerCode?: InputMaybe<SortEnumType>;
  invoiceDiscCode?: InputMaybe<SortEnumType>;
  languageCode?: InputMaybe<SortEnumType>;
  lastDateModified?: InputMaybe<SortEnumType>;
  leadTimeCalculation?: InputMaybe<SortEnumType>;
  locationCode?: InputMaybe<SortEnumType>;
  lstNo?: InputMaybe<SortEnumType>;
  msmeRegNo?: InputMaybe<SortEnumType>;
  name?: InputMaybe<SortEnumType>;
  name2?: InputMaybe<SortEnumType>;
  nameOnInvoice?: InputMaybe<SortEnumType>;
  no?: InputMaybe<SortEnumType>;
  noSeries?: InputMaybe<SortEnumType>;
  ourAccountNo?: InputMaybe<SortEnumType>;
  panNo?: InputMaybe<SortEnumType>;
  panReferenceNo?: InputMaybe<SortEnumType>;
  panStatus?: InputMaybe<SortEnumType>;
  partnerType?: InputMaybe<SortEnumType>;
  payToVendorNo?: InputMaybe<SortEnumType>;
  paymentMethodCode?: InputMaybe<SortEnumType>;
  paymentTermsCode?: InputMaybe<SortEnumType>;
  phoneNo?: InputMaybe<SortEnumType>;
  postCode?: InputMaybe<SortEnumType>;
  preferredBankAccount?: InputMaybe<SortEnumType>;
  prepayment?: InputMaybe<SortEnumType>;
  pricesIncludingVAT?: InputMaybe<SortEnumType>;
  primaryContactNo?: InputMaybe<SortEnumType>;
  priority?: InputMaybe<SortEnumType>;
  purchaserCode?: InputMaybe<SortEnumType>;
  range?: InputMaybe<SortEnumType>;
  responsibilityCenter?: InputMaybe<SortEnumType>;
  searchName?: InputMaybe<SortEnumType>;
  selfInvoice?: InputMaybe<SortEnumType>;
  serviceEntityType?: InputMaybe<SortEnumType>;
  serviceTaxRegistrationNo?: InputMaybe<SortEnumType>;
  shipmentMethodCode?: InputMaybe<SortEnumType>;
  shippingAgentCode?: InputMaybe<SortEnumType>;
  ssi?: InputMaybe<SortEnumType>;
  ssiValidityDate?: InputMaybe<SortEnumType>;
  stateCode?: InputMaybe<SortEnumType>;
  statisticsGroup?: InputMaybe<SortEnumType>;
  stopAutoCorrectName?: InputMaybe<SortEnumType>;
  structure?: InputMaybe<SortEnumType>;
  subcontractor?: InputMaybe<SortEnumType>;
  taxAreaCode?: InputMaybe<SortEnumType>;
  taxLiable?: InputMaybe<SortEnumType>;
  tcsOnPurchase?: InputMaybe<SortEnumType>;
  telexAnswerBack?: InputMaybe<SortEnumType>;
  telexNo?: InputMaybe<SortEnumType>;
  territoryCode?: InputMaybe<SortEnumType>;
  tinNo?: InputMaybe<SortEnumType>;
  transporter?: InputMaybe<SortEnumType>;
  vatBusPostingGroup?: InputMaybe<SortEnumType>;
  vatRegistrationNo?: InputMaybe<SortEnumType>;
  vendorLocation?: InputMaybe<SortEnumType>;
  vendorPostingGroup?: InputMaybe<SortEnumType>;
  vendorType?: InputMaybe<SortEnumType>;
};

export type GetSessionsQueryVariables = Exact<{
  userId?: InputMaybe<Scalars['String']['input']>;
}>;


export type GetSessionsQuery = { __typename?: 'Query', sessions: Array<{ __typename?: 'SessionInfo', sessionId: string, userId: string, userType: string, entityType: string, entityCode: string, department: string, createdAtUtc: any, expiresAtUtc: any }> };

export type KillSessionMutationVariables = Exact<{
  sessionId: Scalars['String']['input'];
}>;


export type KillSessionMutation = { __typename?: 'Mutation', killSession: { __typename?: 'KillSessionResult', success: boolean, message: string } };

export type KillSessionsByUserMutationVariables = Exact<{
  userId: Scalars['String']['input'];
}>;


export type KillSessionsByUserMutation = { __typename?: 'Mutation', killSessionsByUser: { __typename?: 'KillSessionsByUserResult', success: boolean, killedCount: number } };

export type ChangePasswordMutationVariables = Exact<{
  userId: Scalars['String']['input'];
  newPassword: Scalars['String']['input'];
  oldPassword?: InputMaybe<Scalars['String']['input']>;
  securityPin?: InputMaybe<Scalars['Int']['input']>;
}>;


export type ChangePasswordMutation = { __typename?: 'Mutation', changePassword: { __typename?: 'ChangePasswordResult', success: boolean, message: string } };

export type ForgotPasswordMutationVariables = Exact<{
  username: Scalars['String']['input'];
  securityPin: Scalars['Int']['input'];
  newPassword: Scalars['String']['input'];
}>;


export type ForgotPasswordMutation = { __typename?: 'Mutation', forgotPassword: { __typename?: 'ChangePasswordResult', success: boolean, message: string } };

export type LoginMutationVariables = Exact<{
  username: Scalars['String']['input'];
  password: Scalars['String']['input'];
  platform?: InputMaybe<Scalars['String']['input']>;
}>;


export type LoginMutation = { __typename?: 'Mutation', login: { __typename?: 'LoginResult', success: boolean, message?: string | null, token?: string | null, requirePasswordChange: boolean, requirePasswordChangeReason?: string | null, user?: { __typename?: 'LoginUser', userId: string, userSecurityId: any, fullName: string, userType: string, title: string, entityType: string, entityCode: string, department: string, respCenter: string, workDate: any, avatar: number } | null, locations?: Array<{ __typename?: 'UserLocation', code: string, name: string, sale: any, purchase: any, production: any, payroll: any }> | null, menus?: Array<{ __typename?: 'Menu', label: string, icon: string, subMenus: Array<{ __typename?: 'SubMenu', label: string, icon: string, items: Array<{ __typename?: 'MenuItem', label: string, icon: string, action: string, order: number, options: string }> }> }> | null } };

export type CreateCalendarEventMutationVariables = Exact<{
  input: CreateEventInput;
}>;


export type CreateCalendarEventMutation = { __typename?: 'Mutation', createCalendarEvent?: { __typename?: 'CalendarEventDto', id: any, title: string, startUtc: any, endUtc: any, eventTypeId?: number | null, eventTypeName?: string | null, eventTypeColor?: string | null, tags: Array<{ __typename?: 'EventTagDto', tagType: EventTagType, tagKey: string, displayName?: string | null }>, reminders: Array<{ __typename?: 'ReminderDto', id: any, remindAtUtc: any, channel: ReminderChannel }> } | null };

export type DeleteCalendarEventMutationVariables = Exact<{
  eventId: Scalars['UUID']['input'];
}>;


export type DeleteCalendarEventMutation = { __typename?: 'Mutation', deleteCalendarEvent: boolean };

export type GetCalendarEventByIdQueryVariables = Exact<{
  eventId: Scalars['UUID']['input'];
}>;


export type GetCalendarEventByIdQuery = { __typename?: 'Query', getCalendarEventById?: { __typename?: 'CalendarEventDto', id: any, ownerUserId: string, eventTypeId?: number | null, eventTypeName?: string | null, eventTypeColor?: string | null, title: string, description?: string | null, startUtc: any, endUtc: any, isAllDay: boolean, timeZoneId?: string | null, location?: string | null, recurrenceRuleId?: any | null, parentEventId?: any | null, tags: Array<{ __typename?: 'EventTagDto', tagType: EventTagType, tagKey: string, displayName?: string | null }>, reminders: Array<{ __typename?: 'ReminderDto', id: any, remindAtUtc: any, channel: ReminderChannel, isSent: boolean, snoozeUntilUtc?: any | null }>, recurrence?: { __typename?: 'RecurrenceDto', frequency: RecurrenceFrequency, interval: number, daysOfWeek?: string | null, dayOfMonth?: number | null, monthOfYear?: number | null, endByDate?: any | null, occurrenceCount?: number | null, rRule?: string | null } | null } | null };

export type GetEventTypesQueryVariables = Exact<{ [key: string]: never; }>;


export type GetEventTypesQuery = { __typename?: 'Query', getEventTypes: Array<{ __typename?: 'EventTypeDto', id: number, name: string, color?: string | null, icon?: string | null, sortOrder: number }> };

export type GetMyCalendarEventsQueryVariables = Exact<{
  fromUtc: Scalars['DateTime']['input'];
  toUtc: Scalars['DateTime']['input'];
  tagType?: InputMaybe<EventTagType>;
  tagKey?: InputMaybe<Scalars['String']['input']>;
}>;


export type GetMyCalendarEventsQuery = { __typename?: 'Query', getMyCalendarEvents: Array<{ __typename?: 'CalendarEventDto', id: any, ownerUserId: string, eventTypeId?: number | null, eventTypeName?: string | null, eventTypeColor?: string | null, title: string, description?: string | null, startUtc: any, endUtc: any, isAllDay: boolean, timeZoneId?: string | null, location?: string | null, recurrenceRuleId?: any | null, parentEventId?: any | null, tags: Array<{ __typename?: 'EventTagDto', tagType: EventTagType, tagKey: string, displayName?: string | null }>, reminders: Array<{ __typename?: 'ReminderDto', id: any, remindAtUtc: any, channel: ReminderChannel, isSent: boolean, snoozeUntilUtc?: any | null }> }> };

export type GetUpcomingRemindersQueryVariables = Exact<{
  untilUtc?: InputMaybe<Scalars['DateTime']['input']>;
}>;


export type GetUpcomingRemindersQuery = { __typename?: 'Query', getUpcomingReminders: Array<{ __typename?: 'CalendarEventDto', id: any, title: string, startUtc: any, endUtc: any, reminders: Array<{ __typename?: 'ReminderDto', id: any, remindAtUtc: any, channel: ReminderChannel, snoozeUntilUtc?: any | null }> }> };

export type GetNotificationsQueryVariables = Exact<{
  limit?: InputMaybe<Scalars['Int']['input']>;
}>;


export type GetNotificationsQuery = { __typename?: 'Query', notifications: Array<{ __typename?: 'Notification', id: any, userId: string, title: string, message: string, type: NotificationType, link?: string | null, isRead: boolean, createdAt: any }> };

export type GetUnreadNotificationCountQueryVariables = Exact<{ [key: string]: never; }>;


export type GetUnreadNotificationCountQuery = { __typename?: 'Query', unreadCount: number };

export type MarkNotificationAsReadMutationVariables = Exact<{
  notificationId: Scalars['UUID']['input'];
}>;


export type MarkNotificationAsReadMutation = { __typename?: 'Mutation', markNotificationAsRead: boolean };

export type MarkAllNotificationsAsReadMutationVariables = Exact<{ [key: string]: never; }>;


export type MarkAllNotificationsAsReadMutation = { __typename?: 'Mutation', markAllNotificationsAsRead: boolean };

export type OnNotificationSubscriptionVariables = Exact<{
  userId: Scalars['String']['input'];
}>;


export type OnNotificationSubscription = { __typename?: 'Subscription', onNotification: { __typename?: 'Notification', id: any, userId: string, title: string, message: string, type: NotificationType, link?: string | null, isRead: boolean, createdAt: any } };

export type SnoozeReminderMutationVariables = Exact<{
  reminderId: Scalars['UUID']['input'];
  snoozeUntilUtc: Scalars['DateTime']['input'];
}>;


export type SnoozeReminderMutation = { __typename?: 'Mutation', snoozeReminder: boolean };

export type UpdateCalendarEventMutationVariables = Exact<{
  eventId: Scalars['UUID']['input'];
  input: UpdateEventInput;
}>;


export type UpdateCalendarEventMutation = { __typename?: 'Mutation', updateCalendarEvent?: { __typename?: 'CalendarEventDto', id: any, title: string, startUtc: any, endUtc: any, eventTypeId?: number | null, eventTypeName?: string | null, tags: Array<{ __typename?: 'EventTagDto', tagType: EventTagType, tagKey: string, displayName?: string | null }>, reminders: Array<{ __typename?: 'ReminderDto', id: any, remindAtUtc: any, channel: ReminderChannel }> } | null };

export type GetGroupDetailsQueryVariables = Exact<{
  category: Scalars['String']['input'];
  codes?: InputMaybe<Scalars['String']['input']>;
}>;


export type GetGroupDetailsQuery = { __typename?: 'Query', groupDetails: Array<{ __typename?: 'GroupDetails', code?: string | null, category?: string | null, name?: string | null, value?: string | null, extraValue?: string | null }> };

export type GetMyBalanceQueryVariables = Exact<{
  entityType?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
}>;


export type GetMyBalanceQuery = { __typename?: 'Query', myBalance: Array<{ __typename?: 'EntityBalance', code: string, balance: any }> };

export type GetMyAreasQueryVariables = Exact<{
  entityType?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  department?: InputMaybe<Scalars['String']['input']>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<AreaFilterInput>;
}>;


export type GetMyAreasQuery = { __typename?: 'Query', myAreas?: { __typename?: 'MyAreasConnection', totalCount: number, nodes?: Array<{ __typename?: 'Area', code?: string | null, name?: string | null }> | null, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null } } | null };

export type GetMyCustomersQueryVariables = Exact<{
  entityType?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  department?: InputMaybe<Scalars['String']['input']>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<CustomerFilterInput>;
}>;


export type GetMyCustomersQuery = { __typename?: 'Query', myCustomers?: { __typename?: 'MyCustomersConnection', totalCount: number, nodes?: Array<{ __typename?: 'Customer', no?: string | null, name?: string | null, city?: string | null, balance: any, address?: string | null, address2?: string | null, postCode?: string | null, stateCode?: string | null, areaCode?: string | null, dealerCode?: string | null, gstRegistrationNo?: string | null, gstStatus: number, panNo?: string | null, customerPriceGroup?: string | null }> | null, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null } } | null };

export type GetDealersQueryVariables = Exact<{
  entityType?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  department?: InputMaybe<Scalars['String']['input']>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  take?: InputMaybe<Scalars['Int']['input']>;
}>;


export type GetDealersQuery = { __typename?: 'Query', dealers?: { __typename?: 'MyDealersConnection', totalCount: number, items?: Array<{ __typename?: 'SalespersonPurchaser', code?: string | null, name?: string | null, depot?: string | null, eMail?: string | null, dealershipName?: string | null, phoneNo?: string | null }> | null } | null };

export type GetMyRegionsQueryVariables = Exact<{
  entityType?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  department?: InputMaybe<Scalars['String']['input']>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<TerritoryFilterInput>;
}>;


export type GetMyRegionsQuery = { __typename?: 'Query', myRegions?: { __typename?: 'MyRegionsConnection', totalCount: number, nodes?: Array<{ __typename?: 'Territory', code?: string | null, name?: string | null }> | null, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null } } | null };

export type GetTransactionsPageQueryVariables = Exact<{
  entityType?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  order?: InputMaybe<Array<AccountTransactionSortInput> | AccountTransactionSortInput>;
}>;


export type GetTransactionsPageQuery = { __typename?: 'Query', myTransactions?: { __typename?: 'MyTransactionsConnection', totalCount: number, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null }, nodes?: Array<{ __typename?: 'AccountTransaction', date?: any | null, type: number, documentNo: string, amount: any, customerNo: string, customerName: string, balance: any }> | null } | null };

export type GetMyTransactionsQueryVariables = Exact<{
  entityType?: InputMaybe<Scalars['String']['input']>;
  entityCode?: InputMaybe<Scalars['String']['input']>;
  respCenter?: InputMaybe<Scalars['String']['input']>;
}>;


export type GetMyTransactionsQuery = { __typename?: 'Query', myTransactions?: { __typename?: 'MyTransactionsConnection', nodes?: Array<{ __typename?: 'AccountTransaction', date?: any | null, type: number, documentNo: string, amount: any, customerNo: string, customerName: string, balance: any }> | null } | null };

export type GetProductionItemNosQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionItemNosQuery = { __typename?: 'Query', productionItemNos: Array<{ __typename?: 'CasingItem', code: string, minRate: string, maxRate: string, category: string }> };

export type GetProductionMakesQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionMakesQuery = { __typename?: 'Query', productionMakes: Array<{ __typename?: 'CodeName', code: string, name: string }> };

export type GetProductionMakeSubMakeQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionMakeSubMakeQuery = { __typename?: 'Query', productionMakeSubMake: Array<{ __typename?: 'CodeName', code: string, name: string }> };

export type GetProductionVendorsCodeNamesQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionVendorsCodeNamesQuery = { __typename?: 'Query', productionVendorsCodeNames: Array<{ __typename?: 'CodeName', code: string, name: string }> };

export type GetProductionInspectorCodeNamesQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionInspectorCodeNamesQuery = { __typename?: 'Query', productionInspectorCodeNames: Array<{ __typename?: 'CodeName', code: string, name: string }> };

export type GetProductionProcurementInspectionQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionProcurementInspectionQuery = { __typename?: 'Query', productionProcurementInspection: Array<{ __typename?: 'CodeName', code: string, name: string }> };

export type GetProductionProcurementMarketsQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionProcurementMarketsQuery = { __typename?: 'Query', productionProcurementMarkets: Array<{ __typename?: 'CodeName', code: string, name: string }> };

export type GetProductionVendorsQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionVendorsQuery = { __typename?: 'Query', productionVendors: Array<{ __typename?: 'VendorModel', no: string, name: string, address: string, address2: string, city: string, category: string, detail: string, respCenter: string, mobileNo: string, ecoMgrCode: string, nameOnInvoice: string, bankName: string, bankIFSC: string, bankAccNo: string, postCode: string, bankBranch: string, selfInvoice: boolean, panNo: string, adhaarNo: string, balance: any, stateCode: string }> };

export type GetProductionEcomileLastNewNumberQueryVariables = Exact<{
  respCenter: Scalars['String']['input'];
}>;


export type GetProductionEcomileLastNewNumberQuery = { __typename?: 'Query', productionEcomileLastNewNumber: string };

export type GetProductionProcurementOrdersInfoQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionProcurementOrdersInfoQuery = { __typename?: 'Query', productionProcurementOrdersInfo: Array<{ __typename?: 'OrderInfo', orderNo: string, supplier: string, supplierCode: string, respCenter: string, status: number, location: string, managerCode: string, date: string, manager: string, qty: number, amount: any }> };

export type GetProductionProcurementOrderLinesDispatchQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionProcurementOrderLinesDispatchQuery = { __typename?: 'Query', productionProcurementOrderLinesDispatch: Array<{ __typename?: 'OrderLineDispatch', orderNo: string, lineNo: number, no: string, make: string, serialNo: string, dispatchOrderNo: string, dispatchDate: string, dispatchDestination: string, dispatchVehicleNo: string, dispatchMobileNo: string, dispatchTransporter: string, button: string, model: string, factInspection: string, newSerialNo: string, rejectionReason: string, supplier: string, location: string, sortNo: string, date: string, inspection: string, orderStatus: string, inspector: string, factInspector: string, factInspectorFinal: string, remark: string }> };

export type GetProductionProcurementOrderLinesNewNumberingQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionProcurementOrderLinesNewNumberingQuery = { __typename?: 'Query', productionProcurementOrderLinesNewNumbering: Array<{ __typename?: 'OrderLineDispatch', orderNo: string, lineNo: number, no: string, make: string, serialNo: string, dispatchOrderNo: string, dispatchDate: string, dispatchDestination: string, dispatchVehicleNo: string, dispatchMobileNo: string, dispatchTransporter: string, button: string, model: string, factInspection: string, newSerialNo: string, rejectionReason: string, supplier: string, location: string, sortNo: string, date: string, inspection: string, orderStatus: string, inspector: string, factInspector: string, factInspectorFinal: string, remark: string }> };

export type GetProductionProcurementOrderLinesQueryVariables = Exact<{
  param: OrderInfoInput;
}>;


export type GetProductionProcurementOrderLinesQuery = { __typename?: 'Query', productionProcurementOrderLines: Array<{ __typename?: 'OrderLine', no: string, lineNo: number, vendorNo: string, itemNo: string, make: string, serialNo: string, amount: any, subMake: string, inspectorCode: string, sortNo: string, inspection: string, inspector: string }> };

export type GetProductionProcurementDispatchOrdersQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionProcurementDispatchOrdersQuery = { __typename?: 'Query', productionProcurementDispatchOrders: Array<{ __typename?: 'DispatchOrder', no: string, mobileNo: string, vehicleNo: string, destination: string, date: string, tyres: number, status: string }> };

export type GetProductionProcMarketsQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionProcMarketsQuery = { __typename?: 'Query', productionProcMarkets: Array<string> };

export type GetProductionEcomileProcurementTilesQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionEcomileProcurementTilesQuery = { __typename?: 'Query', productionEcomileProcurementTiles: Array<{ __typename?: 'Tile', label: string, value: any, description: string }> };

export type GetProductionShipmentOrderForMergerQueryVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GetProductionShipmentOrderForMergerQuery = { __typename?: 'Query', productionShipmentOrderForMerger: Array<{ __typename?: 'ShipmentInfo', no: string, destination: string, mobileNo: string, transport: string, vehicleNo: string, date: string }> };

export type UpdateProductionCasingMutationVariables = Exact<{
  param: FetchParamsInput;
}>;


export type UpdateProductionCasingMutation = { __typename?: 'Mutation', updateProductionCasing: { __typename?: 'MutationResult', success: boolean, message: string } };

export type InsertProductionCasingItemsMutationVariables = Exact<{
  casingItems: Array<CasingItemInput> | CasingItemInput;
}>;


export type InsertProductionCasingItemsMutation = { __typename?: 'Mutation', insertProductionCasingItems: { __typename?: 'MutationResult', success: boolean, message: string } };

export type UpdateProductionVendorMutationVariables = Exact<{
  param: VendorModelInput;
}>;


export type UpdateProductionVendorMutation = { __typename?: 'Mutation', updateProductionVendor: { __typename?: 'MutationResult', success: boolean, message: string } };

export type CreateProductionVendorMutationVariables = Exact<{
  param: FetchParamsInput;
}>;


export type CreateProductionVendorMutation = { __typename?: 'Mutation', createProductionVendor: string };

export type NewProductionProcurementOrderMutationVariables = Exact<{
  param: FetchParamsInput;
}>;


export type NewProductionProcurementOrderMutation = { __typename?: 'Mutation', newProductionProcurementOrder: string };

export type UpdateProductionProcurementOrderMutationVariables = Exact<{
  order: OrderInfoInput;
}>;


export type UpdateProductionProcurementOrderMutation = { __typename?: 'Mutation', updateProductionProcurementOrder: number };

export type NewProductionProcShipNoMutationVariables = Exact<{
  param: FetchParamsInput;
}>;


export type NewProductionProcShipNoMutation = { __typename?: 'Mutation', newProductionProcShipNo: string };

export type GenerateProductionGrAsMutationVariables = Exact<{
  param: FetchParamsInput;
}>;


export type GenerateProductionGrAsMutation = { __typename?: 'Mutation', generateProductionGRAs: string };

export type InsertProductionProcurementOrderLineMutationVariables = Exact<{
  order: OrderLineInput;
}>;


export type InsertProductionProcurementOrderLineMutation = { __typename?: 'Mutation', insertProductionProcurementOrderLine: number };

export type UpdateProductionProcurementOrderLineMutationVariables = Exact<{
  order: OrderLineInput;
}>;


export type UpdateProductionProcurementOrderLineMutation = { __typename?: 'Mutation', updateProductionProcurementOrderLine: number };

export type UpdateProductionProcOrdLineDispatchMutationVariables = Exact<{
  lines: Array<OrderLineDispatchInput> | OrderLineDispatchInput;
}>;


export type UpdateProductionProcOrdLineDispatchMutation = { __typename?: 'Mutation', updateProductionProcOrdLineDispatch: number };

export type UpdateProductionProcOrdLineDispatchSingleMutationVariables = Exact<{
  line: OrderLineDispatchInput;
}>;


export type UpdateProductionProcOrdLineDispatchSingleMutation = { __typename?: 'Mutation', updateProductionProcOrdLineDispatchSingle: number };

export type UpdateProductionProcOrdLineReceiptMutationVariables = Exact<{
  lines: Array<OrderLineDispatchInput> | OrderLineDispatchInput;
}>;


export type UpdateProductionProcOrdLineReceiptMutation = { __typename?: 'Mutation', updateProductionProcOrdLineReceipt: number };

export type UpdateProductionProcOrdLineRemoveMutationVariables = Exact<{
  lines: Array<OrderLineDispatchInput> | OrderLineDispatchInput;
}>;


export type UpdateProductionProcOrdLineRemoveMutation = { __typename?: 'Mutation', updateProductionProcOrdLineRemove: number };

export type UpdateProductionProcOrdLineDropMutationVariables = Exact<{
  lines: Array<OrderLineDispatchInput> | OrderLineDispatchInput;
}>;


export type UpdateProductionProcOrdLineDropMutation = { __typename?: 'Mutation', updateProductionProcOrdLineDrop: number };

export type DeleteProductionProcurementOrderLineMutationVariables = Exact<{
  order: OrderLineInput;
}>;


export type DeleteProductionProcurementOrderLineMutation = { __typename?: 'Mutation', deleteProductionProcurementOrderLine: number };

export type DeleteProductionProcurementOrderMutationVariables = Exact<{
  order: OrderInfoInput;
}>;


export type DeleteProductionProcurementOrderMutation = { __typename?: 'Mutation', deleteProductionProcurementOrder: number };

export type GetMyVendorsQueryVariables = Exact<{
  respCenter?: InputMaybe<Scalars['String']['input']>;
  categories?: InputMaybe<Array<Scalars['String']['input']> | Scalars['String']['input']>;
  ecoMgr?: InputMaybe<Scalars['String']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  where?: InputMaybe<VendorFilterInput>;
  order?: InputMaybe<Array<VendorSortInput> | VendorSortInput>;
}>;


export type GetMyVendorsQuery = { __typename?: 'Query', myVendors?: { __typename?: 'MyVendorsConnection', totalCount: number, items?: Array<{ __typename?: 'Vendor', no?: string | null, name?: string | null, city?: string | null, address?: string | null, address2?: string | null, postCode?: string | null, stateCode?: string | null, contact?: string | null, gstRegistrationNo?: string | null, gstStatus: number, panNo?: string | null, nameOnInvoice?: string | null, bankName?: string | null, bankACNo?: string | null, bankIFSCCode?: string | null, bankBranch?: string | null, groupCategory?: string | null, groupDetails?: string | null, ecomileProcMgr?: string | null, territoryCode?: string | null, phoneNo?: string | null, responsibilityCenter?: string | null, blocked: number, balance?: any | null, email?: string | null }> | null, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, hasPreviousPage: boolean, startCursor?: string | null, endCursor?: string | null } } | null };

export type GetUserQueryVariables = Exact<{
  username: Scalars['String']['input'];
}>;


export type GetUserQuery = { __typename?: 'Query', user?: { __typename?: 'UserDetail', userId: string, fullName: string, rdpPassword?: string | null, navConfigName?: string | null } | null };

export type GetProfileQueryVariables = Exact<{
  userId: Scalars['String']['input'];
}>;


export type GetProfileQuery = { __typename?: 'Query', profile?: { __typename?: 'ProfileResult', userId: string, fullName: string, userType: string, mobileNo: string, email: string, avatar: number, lastPasswordChanged: any, securityPIN: number, entities: Array<{ __typename?: 'UserEntity', code: string, name: string, title: string, location: string }> } | null };

export type SetProfileMutationVariables = Exact<{
  userId: Scalars['String']['input'];
  input: ProfileUpdateInput;
}>;


export type SetProfileMutation = { __typename?: 'Mutation', setProfile: { __typename?: 'SetProfileResult', success: boolean, message: string } };

export type GetPostCodesQueryVariables = Exact<{
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<PostCodeFilterInput>;
}>;


export type GetPostCodesQuery = { __typename?: 'Query', postCodes?: { __typename?: 'PostCodesConnection', totalCount: number, items?: Array<{ __typename?: 'PostCode', code?: string | null, city?: string | null, searchCity?: string | null, stateCode?: string | null, county?: string | null, countryRegionCode?: string | null }> | null, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, hasPreviousPage: boolean, startCursor?: string | null, endCursor?: string | null } } | null };

export type GetEcoprocTilesQueryVariables = Exact<{
  statusPosted: Scalars['Int']['input'];
  userCode?: InputMaybe<Scalars['String']['input']>;
  userDepartment?: InputMaybe<Scalars['String']['input']>;
  userSpecialToken?: InputMaybe<Scalars['String']['input']>;
  respCenters?: InputMaybe<Scalars['String']['input']>;
}>;


export type GetEcoprocTilesQuery = { __typename?: 'Query', open?: { __typename?: 'ProcurementOrdersConnection', totalCount: number } | null, posted?: { __typename?: 'ProcurementOrdersConnection', totalCount: number } | null, vendors?: { __typename?: 'MyVendorsConnection', totalCount: number } | null };

export type GetProcurementOrdersQueryVariables = Exact<{
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  order?: InputMaybe<Array<PurchaseHeaderSortInput> | PurchaseHeaderSortInput>;
  where?: InputMaybe<PurchaseHeaderFilterInput>;
  userCode?: InputMaybe<Scalars['String']['input']>;
  userDepartment?: InputMaybe<Scalars['String']['input']>;
  userSpecialToken?: InputMaybe<Scalars['String']['input']>;
  respCenters?: InputMaybe<Scalars['String']['input']>;
  statusFilter?: InputMaybe<Scalars['Int']['input']>;
}>;


export type GetProcurementOrdersQuery = { __typename?: 'Query', procurementOrders?: { __typename?: 'ProcurementOrdersConnection', totalCount: number, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null }, nodes?: Array<{ __typename?: 'PurchaseHeader', no?: string | null, postingDate?: any | null, orderDate?: any | null, buyFromVendorName?: string | null, buyFromVendorNo?: string | null, responsibilityCenter?: string | null, orderStatus: number, ecomileProcMgr?: string | null }> | null } | null };

export type GetProcurementOrderLinesQueryVariables = Exact<{
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  order?: InputMaybe<Array<PurchaseLineSortInput> | PurchaseLineSortInput>;
  where?: InputMaybe<PurchaseLineFilterInput>;
  userCode?: InputMaybe<Scalars['String']['input']>;
  userDepartment?: InputMaybe<Scalars['String']['input']>;
  userSpecialToken?: InputMaybe<Scalars['String']['input']>;
  respCenters?: InputMaybe<Scalars['String']['input']>;
  statusFilter?: InputMaybe<Scalars['Int']['input']>;
}>;


export type GetProcurementOrderLinesQuery = { __typename?: 'Query', procurementOrderLines?: { __typename?: 'ProcurementOrderLinesConnection', totalCount: number, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null }, nodes?: Array<{ __typename?: 'PurchaseLine', documentNo?: string | null, lineNo: number, no?: string | null, make?: string | null, serialNo?: string | null, dispatchOrderNo?: string | null, dispatchDate?: any | null, dispatchDestination?: string | null, dispatchVehicleNo?: string | null, dispatchMobileNo?: string | null, dispatchTransporter?: string | null, model?: string | null, inspection?: string | null, newSerialNo?: string | null, rejectionReason?: string | null, orderDate?: any | null, casingCondition: number, orderStatus: number }> | null } | null };

export type GetEcomileLastNewNumberQueryVariables = Exact<{
  respCenter: Scalars['String']['input'];
}>;


export type GetEcomileLastNewNumberQuery = { __typename?: 'Query', productionEcomileLastNewNumber: string };

export type GetProcurementNewNumberingPagedQueryVariables = Exact<{
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  fromDate?: InputMaybe<Scalars['DateTime']['input']>;
  toDate?: InputMaybe<Scalars['DateTime']['input']>;
  respCenters?: InputMaybe<Scalars['String']['input']>;
  view?: InputMaybe<Scalars['String']['input']>;
  type?: InputMaybe<Scalars['String']['input']>;
  nos?: InputMaybe<Array<Scalars['String']['input']> | Scalars['String']['input']>;
  where?: InputMaybe<ProcurementNewNumberingDtoFilterInput>;
  order?: InputMaybe<Array<ProcurementNewNumberingDtoSortInput> | ProcurementNewNumberingDtoSortInput>;
}>;


export type GetProcurementNewNumberingPagedQuery = { __typename?: 'Query', procurementNewNumberingPaged?: { __typename?: 'ProcurementNewNumberingPagedConnection', totalCount: number, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null }, nodes?: Array<{ __typename?: 'ProcurementNewNumberingDto', orderNo: string, lineNo: number, no: string, make: string, serialNo: string, dispatchOrderNo: string, dispatchDate?: any | null, dispatchDestination: string, dispatchVehicleNo: string, dispatchMobileNo: string, dispatchTransporter: string, button: string, model: string, newSerialNo: string, factInspection: string, rejectionReason: string, supplier: string, location: string, date?: any | null, inspector: string, factInspector: string, factInspectorFinal: string, sortNo: string, inspection: string, orderStatus: string, remark: string }> | null } | null };

export type GetRespCentersMockQueryVariables = Exact<{ [key: string]: never; }>;


export type GetRespCentersMockQuery = { __typename: 'Query' };

export type GetRespCenterMockQueryVariables = Exact<{ [key: string]: never; }>;


export type GetRespCenterMockQuery = { __typename: 'Query' };

export type GetUsersQueryVariables = Exact<{
  skip?: InputMaybe<Scalars['Int']['input']>;
  take?: InputMaybe<Scalars['Int']['input']>;
  where?: InputMaybe<Scalars['String']['input']>;
  order?: InputMaybe<Scalars['String']['input']>;
}>;


export type GetUsersQuery = { __typename?: 'Query', user?: { __typename?: 'UserDetail', userId: string } | null };

export type GetResponsibilityCentersQueryVariables = Exact<{
  natureOfBusiness?: InputMaybe<Array<Scalars['Int']['input']> | Scalars['Int']['input']>;
  skip?: InputMaybe<Scalars['Int']['input']>;
  take?: InputMaybe<Scalars['Int']['input']>;
}>;


export type GetResponsibilityCentersQuery = { __typename: 'Query' };

export type GetEmployeesQueryVariables = Exact<{
  skip?: InputMaybe<Scalars['Int']['input']>;
  take?: InputMaybe<Scalars['Int']['input']>;
}>;


export type GetEmployeesQuery = { __typename: 'Query' };

export type GetPermissionSetsQueryVariables = Exact<{
  take?: InputMaybe<Scalars['Int']['input']>;
}>;


export type GetPermissionSetsQuery = { __typename: 'Query' };

export type UpdateUserPermissionsMutationVariables = Exact<{ [key: string]: never; }>;


export type UpdateUserPermissionsMutation = { __typename: 'Mutation' };

export type UpdateUserResponsibilityCentersMutationVariables = Exact<{ [key: string]: never; }>;


export type UpdateUserResponsibilityCentersMutation = { __typename: 'Mutation' };

export type UpdateUserPostingSetupMutationVariables = Exact<{ [key: string]: never; }>;


export type UpdateUserPostingSetupMutation = { __typename: 'Mutation' };

export type UpdateUserDetailsMutationVariables = Exact<{ [key: string]: never; }>;


export type UpdateUserDetailsMutation = { __typename: 'Mutation' };

export type GetReportsQueryVariables = Exact<{
  category: Scalars['String']['input'];
}>;


export type GetReportsQuery = { __typename: 'Query' };

export type GetDealerByCodeQueryVariables = Exact<{
  code: Scalars['String']['input'];
}>;


export type GetDealerByCodeQuery = { __typename?: 'Query', dealerByCode: { __typename?: 'SalespersonPurchaser', code?: string | null, name?: string | null, dealershipName?: string | null, eMail?: string | null, mobileNo?: string | null, businessModel: number, product: number, status: number, investmentAmount: any, dealershipExpDate?: any | null, dealershipStartDate?: any | null, dateOfBirth?: any | null, dateOfAniversary?: any | null, brandedShop: number, panNo?: string | null, gstNo?: string | null, aadharNo?: string | null, bankName?: string | null, bankACNo?: string | null, bankBranch?: string | null, bankIFSC?: string | null } };

export type SaveDealerMutationVariables = Exact<{
  input: SaveDealerInput;
}>;


export type SaveDealerMutation = { __typename?: 'Mutation', saveDealer: { __typename?: 'MutationResult', success: boolean, message: string } };


export const GetSessionsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetSessions"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userId"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"sessions"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"userId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userId"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"sessionId"}},{"kind":"Field","name":{"kind":"Name","value":"userId"}},{"kind":"Field","name":{"kind":"Name","value":"userType"}},{"kind":"Field","name":{"kind":"Name","value":"entityType"}},{"kind":"Field","name":{"kind":"Name","value":"entityCode"}},{"kind":"Field","name":{"kind":"Name","value":"department"}},{"kind":"Field","name":{"kind":"Name","value":"createdAtUtc"}},{"kind":"Field","name":{"kind":"Name","value":"expiresAtUtc"}}]}}]}}]} as unknown as DocumentNode<GetSessionsQuery, GetSessionsQueryVariables>;
export const KillSessionDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"KillSession"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"sessionId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"killSession"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"sessionId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"sessionId"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"message"}}]}}]}}]} as unknown as DocumentNode<KillSessionMutation, KillSessionMutationVariables>;
export const KillSessionsByUserDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"KillSessionsByUser"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"killSessionsByUser"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"userId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userId"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"killedCount"}}]}}]}}]} as unknown as DocumentNode<KillSessionsByUserMutation, KillSessionsByUserMutationVariables>;
export const ChangePasswordDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"ChangePassword"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"newPassword"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"oldPassword"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"securityPin"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"changePassword"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"userId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userId"}}},{"kind":"Argument","name":{"kind":"Name","value":"newPassword"},"value":{"kind":"Variable","name":{"kind":"Name","value":"newPassword"}}},{"kind":"Argument","name":{"kind":"Name","value":"oldPassword"},"value":{"kind":"Variable","name":{"kind":"Name","value":"oldPassword"}}},{"kind":"Argument","name":{"kind":"Name","value":"securityPin"},"value":{"kind":"Variable","name":{"kind":"Name","value":"securityPin"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"message"}}]}}]}}]} as unknown as DocumentNode<ChangePasswordMutation, ChangePasswordMutationVariables>;
export const ForgotPasswordDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"ForgotPassword"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"username"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"securityPin"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"newPassword"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"forgotPassword"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"username"},"value":{"kind":"Variable","name":{"kind":"Name","value":"username"}}},{"kind":"Argument","name":{"kind":"Name","value":"securityPin"},"value":{"kind":"Variable","name":{"kind":"Name","value":"securityPin"}}},{"kind":"Argument","name":{"kind":"Name","value":"newPassword"},"value":{"kind":"Variable","name":{"kind":"Name","value":"newPassword"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"message"}}]}}]}}]} as unknown as DocumentNode<ForgotPasswordMutation, ForgotPasswordMutationVariables>;
export const LoginDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"Login"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"username"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"password"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"platform"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"login"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"username"},"value":{"kind":"Variable","name":{"kind":"Name","value":"username"}}},{"kind":"Argument","name":{"kind":"Name","value":"password"},"value":{"kind":"Variable","name":{"kind":"Name","value":"password"}}},{"kind":"Argument","name":{"kind":"Name","value":"platform"},"value":{"kind":"Variable","name":{"kind":"Name","value":"platform"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"message"}},{"kind":"Field","name":{"kind":"Name","value":"token"}},{"kind":"Field","name":{"kind":"Name","value":"requirePasswordChange"}},{"kind":"Field","name":{"kind":"Name","value":"requirePasswordChangeReason"}},{"kind":"Field","name":{"kind":"Name","value":"user"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"userId"}},{"kind":"Field","name":{"kind":"Name","value":"userSecurityId"}},{"kind":"Field","name":{"kind":"Name","value":"fullName"}},{"kind":"Field","name":{"kind":"Name","value":"userType"}},{"kind":"Field","name":{"kind":"Name","value":"title"}},{"kind":"Field","name":{"kind":"Name","value":"entityType"}},{"kind":"Field","name":{"kind":"Name","value":"entityCode"}},{"kind":"Field","name":{"kind":"Name","value":"department"}},{"kind":"Field","name":{"kind":"Name","value":"respCenter"}},{"kind":"Field","name":{"kind":"Name","value":"workDate"}},{"kind":"Field","name":{"kind":"Name","value":"avatar"}}]}},{"kind":"Field","name":{"kind":"Name","value":"locations"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"sale"}},{"kind":"Field","name":{"kind":"Name","value":"purchase"}},{"kind":"Field","name":{"kind":"Name","value":"production"}},{"kind":"Field","name":{"kind":"Name","value":"payroll"}}]}},{"kind":"Field","name":{"kind":"Name","value":"menus"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"label"}},{"kind":"Field","name":{"kind":"Name","value":"icon"}},{"kind":"Field","name":{"kind":"Name","value":"subMenus"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"label"}},{"kind":"Field","name":{"kind":"Name","value":"icon"}},{"kind":"Field","name":{"kind":"Name","value":"items"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"label"}},{"kind":"Field","name":{"kind":"Name","value":"icon"}},{"kind":"Field","name":{"kind":"Name","value":"action"}},{"kind":"Field","name":{"kind":"Name","value":"order"}},{"kind":"Field","name":{"kind":"Name","value":"options"}}]}}]}}]}}]}}]}}]} as unknown as DocumentNode<LoginMutation, LoginMutationVariables>;
export const CreateCalendarEventDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"CreateCalendarEvent"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"input"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"CreateEventInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"createCalendarEvent"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"input"},"value":{"kind":"Variable","name":{"kind":"Name","value":"input"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"title"}},{"kind":"Field","name":{"kind":"Name","value":"startUtc"}},{"kind":"Field","name":{"kind":"Name","value":"endUtc"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeId"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeName"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeColor"}},{"kind":"Field","name":{"kind":"Name","value":"tags"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"tagType"}},{"kind":"Field","name":{"kind":"Name","value":"tagKey"}},{"kind":"Field","name":{"kind":"Name","value":"displayName"}}]}},{"kind":"Field","name":{"kind":"Name","value":"reminders"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"remindAtUtc"}},{"kind":"Field","name":{"kind":"Name","value":"channel"}}]}}]}}]}}]} as unknown as DocumentNode<CreateCalendarEventMutation, CreateCalendarEventMutationVariables>;
export const DeleteCalendarEventDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"DeleteCalendarEvent"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"eventId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"UUID"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"deleteCalendarEvent"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"eventId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"eventId"}}}]}]}}]} as unknown as DocumentNode<DeleteCalendarEventMutation, DeleteCalendarEventMutationVariables>;
export const GetCalendarEventByIdDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetCalendarEventById"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"eventId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"UUID"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"getCalendarEventById"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"eventId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"eventId"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"ownerUserId"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeId"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeName"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeColor"}},{"kind":"Field","name":{"kind":"Name","value":"title"}},{"kind":"Field","name":{"kind":"Name","value":"description"}},{"kind":"Field","name":{"kind":"Name","value":"startUtc"}},{"kind":"Field","name":{"kind":"Name","value":"endUtc"}},{"kind":"Field","name":{"kind":"Name","value":"isAllDay"}},{"kind":"Field","name":{"kind":"Name","value":"timeZoneId"}},{"kind":"Field","name":{"kind":"Name","value":"location"}},{"kind":"Field","name":{"kind":"Name","value":"recurrenceRuleId"}},{"kind":"Field","name":{"kind":"Name","value":"parentEventId"}},{"kind":"Field","name":{"kind":"Name","value":"tags"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"tagType"}},{"kind":"Field","name":{"kind":"Name","value":"tagKey"}},{"kind":"Field","name":{"kind":"Name","value":"displayName"}}]}},{"kind":"Field","name":{"kind":"Name","value":"reminders"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"remindAtUtc"}},{"kind":"Field","name":{"kind":"Name","value":"channel"}},{"kind":"Field","name":{"kind":"Name","value":"isSent"}},{"kind":"Field","name":{"kind":"Name","value":"snoozeUntilUtc"}}]}},{"kind":"Field","name":{"kind":"Name","value":"recurrence"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"frequency"}},{"kind":"Field","name":{"kind":"Name","value":"interval"}},{"kind":"Field","name":{"kind":"Name","value":"daysOfWeek"}},{"kind":"Field","name":{"kind":"Name","value":"dayOfMonth"}},{"kind":"Field","name":{"kind":"Name","value":"monthOfYear"}},{"kind":"Field","name":{"kind":"Name","value":"endByDate"}},{"kind":"Field","name":{"kind":"Name","value":"occurrenceCount"}},{"kind":"Field","name":{"kind":"Name","value":"rRule"}}]}}]}}]}}]} as unknown as DocumentNode<GetCalendarEventByIdQuery, GetCalendarEventByIdQueryVariables>;
export const GetEventTypesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetEventTypes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"getEventTypes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"color"}},{"kind":"Field","name":{"kind":"Name","value":"icon"}},{"kind":"Field","name":{"kind":"Name","value":"sortOrder"}}]}}]}}]} as unknown as DocumentNode<GetEventTypesQuery, GetEventTypesQueryVariables>;
export const GetMyCalendarEventsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetMyCalendarEvents"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"fromUtc"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"DateTime"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"toUtc"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"DateTime"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"tagType"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"EventTagType"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"tagKey"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"getMyCalendarEvents"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"fromUtc"},"value":{"kind":"Variable","name":{"kind":"Name","value":"fromUtc"}}},{"kind":"Argument","name":{"kind":"Name","value":"toUtc"},"value":{"kind":"Variable","name":{"kind":"Name","value":"toUtc"}}},{"kind":"Argument","name":{"kind":"Name","value":"tagType"},"value":{"kind":"Variable","name":{"kind":"Name","value":"tagType"}}},{"kind":"Argument","name":{"kind":"Name","value":"tagKey"},"value":{"kind":"Variable","name":{"kind":"Name","value":"tagKey"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"ownerUserId"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeId"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeName"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeColor"}},{"kind":"Field","name":{"kind":"Name","value":"title"}},{"kind":"Field","name":{"kind":"Name","value":"description"}},{"kind":"Field","name":{"kind":"Name","value":"startUtc"}},{"kind":"Field","name":{"kind":"Name","value":"endUtc"}},{"kind":"Field","name":{"kind":"Name","value":"isAllDay"}},{"kind":"Field","name":{"kind":"Name","value":"timeZoneId"}},{"kind":"Field","name":{"kind":"Name","value":"location"}},{"kind":"Field","name":{"kind":"Name","value":"recurrenceRuleId"}},{"kind":"Field","name":{"kind":"Name","value":"parentEventId"}},{"kind":"Field","name":{"kind":"Name","value":"tags"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"tagType"}},{"kind":"Field","name":{"kind":"Name","value":"tagKey"}},{"kind":"Field","name":{"kind":"Name","value":"displayName"}}]}},{"kind":"Field","name":{"kind":"Name","value":"reminders"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"remindAtUtc"}},{"kind":"Field","name":{"kind":"Name","value":"channel"}},{"kind":"Field","name":{"kind":"Name","value":"isSent"}},{"kind":"Field","name":{"kind":"Name","value":"snoozeUntilUtc"}}]}}]}}]}}]} as unknown as DocumentNode<GetMyCalendarEventsQuery, GetMyCalendarEventsQueryVariables>;
export const GetUpcomingRemindersDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetUpcomingReminders"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"untilUtc"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"DateTime"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"getUpcomingReminders"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"untilUtc"},"value":{"kind":"Variable","name":{"kind":"Name","value":"untilUtc"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"title"}},{"kind":"Field","name":{"kind":"Name","value":"startUtc"}},{"kind":"Field","name":{"kind":"Name","value":"endUtc"}},{"kind":"Field","name":{"kind":"Name","value":"reminders"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"remindAtUtc"}},{"kind":"Field","name":{"kind":"Name","value":"channel"}},{"kind":"Field","name":{"kind":"Name","value":"snoozeUntilUtc"}}]}}]}}]}}]} as unknown as DocumentNode<GetUpcomingRemindersQuery, GetUpcomingRemindersQueryVariables>;
export const GetNotificationsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetNotifications"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"limit"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","alias":{"kind":"Name","value":"notifications"},"name":{"kind":"Name","value":"getNotifications"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"limit"},"value":{"kind":"Variable","name":{"kind":"Name","value":"limit"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"userId"}},{"kind":"Field","name":{"kind":"Name","value":"title"}},{"kind":"Field","name":{"kind":"Name","value":"message"}},{"kind":"Field","name":{"kind":"Name","value":"type"}},{"kind":"Field","name":{"kind":"Name","value":"link"}},{"kind":"Field","name":{"kind":"Name","value":"isRead"}},{"kind":"Field","name":{"kind":"Name","value":"createdAt"}}]}}]}}]} as unknown as DocumentNode<GetNotificationsQuery, GetNotificationsQueryVariables>;
export const GetUnreadNotificationCountDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetUnreadNotificationCount"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","alias":{"kind":"Name","value":"unreadCount"},"name":{"kind":"Name","value":"getUnreadNotificationCount"}}]}}]} as unknown as DocumentNode<GetUnreadNotificationCountQuery, GetUnreadNotificationCountQueryVariables>;
export const MarkNotificationAsReadDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"MarkNotificationAsRead"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"notificationId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"UUID"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"markNotificationAsRead"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"notificationId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"notificationId"}}}]}]}}]} as unknown as DocumentNode<MarkNotificationAsReadMutation, MarkNotificationAsReadMutationVariables>;
export const MarkAllNotificationsAsReadDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"MarkAllNotificationsAsRead"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"markAllNotificationsAsRead"}}]}}]} as unknown as DocumentNode<MarkAllNotificationsAsReadMutation, MarkAllNotificationsAsReadMutationVariables>;
export const OnNotificationDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"subscription","name":{"kind":"Name","value":"OnNotification"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"onNotification"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"userId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userId"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"userId"}},{"kind":"Field","name":{"kind":"Name","value":"title"}},{"kind":"Field","name":{"kind":"Name","value":"message"}},{"kind":"Field","name":{"kind":"Name","value":"type"}},{"kind":"Field","name":{"kind":"Name","value":"link"}},{"kind":"Field","name":{"kind":"Name","value":"isRead"}},{"kind":"Field","name":{"kind":"Name","value":"createdAt"}}]}}]}}]} as unknown as DocumentNode<OnNotificationSubscription, OnNotificationSubscriptionVariables>;
export const SnoozeReminderDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"SnoozeReminder"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"reminderId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"UUID"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"snoozeUntilUtc"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"DateTime"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"snoozeReminder"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"reminderId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"reminderId"}}},{"kind":"Argument","name":{"kind":"Name","value":"snoozeUntilUtc"},"value":{"kind":"Variable","name":{"kind":"Name","value":"snoozeUntilUtc"}}}]}]}}]} as unknown as DocumentNode<SnoozeReminderMutation, SnoozeReminderMutationVariables>;
export const UpdateCalendarEventDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateCalendarEvent"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"eventId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"UUID"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"input"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"UpdateEventInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateCalendarEvent"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"eventId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"eventId"}}},{"kind":"Argument","name":{"kind":"Name","value":"input"},"value":{"kind":"Variable","name":{"kind":"Name","value":"input"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"title"}},{"kind":"Field","name":{"kind":"Name","value":"startUtc"}},{"kind":"Field","name":{"kind":"Name","value":"endUtc"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeId"}},{"kind":"Field","name":{"kind":"Name","value":"eventTypeName"}},{"kind":"Field","name":{"kind":"Name","value":"tags"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"tagType"}},{"kind":"Field","name":{"kind":"Name","value":"tagKey"}},{"kind":"Field","name":{"kind":"Name","value":"displayName"}}]}},{"kind":"Field","name":{"kind":"Name","value":"reminders"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"id"}},{"kind":"Field","name":{"kind":"Name","value":"remindAtUtc"}},{"kind":"Field","name":{"kind":"Name","value":"channel"}}]}}]}}]}}]} as unknown as DocumentNode<UpdateCalendarEventMutation, UpdateCalendarEventMutationVariables>;
export const GetGroupDetailsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetGroupDetails"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"category"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"codes"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"groupDetails"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"category"},"value":{"kind":"Variable","name":{"kind":"Name","value":"category"}}},{"kind":"Argument","name":{"kind":"Name","value":"codes"},"value":{"kind":"Variable","name":{"kind":"Name","value":"codes"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"category"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"value"}},{"kind":"Field","name":{"kind":"Name","value":"extraValue"}}]}}]}}]} as unknown as DocumentNode<GetGroupDetailsQuery, GetGroupDetailsQueryVariables>;
export const GetMyBalanceDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetMyBalance"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"myBalance"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"entityType"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}}},{"kind":"Argument","name":{"kind":"Name","value":"entityCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"balance"}}]}}]}}]} as unknown as DocumentNode<GetMyBalanceQuery, GetMyBalanceQueryVariables>;
export const GetMyAreasDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetMyAreas"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"department"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"AreaFilterInput"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"myAreas"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"entityType"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}}},{"kind":"Argument","name":{"kind":"Name","value":"entityCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"department"},"value":{"kind":"Variable","name":{"kind":"Name","value":"department"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}},{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}}]}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"totalCount"}}]}}]}}]} as unknown as DocumentNode<GetMyAreasQuery, GetMyAreasQueryVariables>;
export const GetMyCustomersDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetMyCustomers"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"department"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"CustomerFilterInput"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"myCustomers"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"entityType"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}}},{"kind":"Argument","name":{"kind":"Name","value":"entityCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"department"},"value":{"kind":"Variable","name":{"kind":"Name","value":"department"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}},{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"city"}},{"kind":"Field","name":{"kind":"Name","value":"balance"}},{"kind":"Field","name":{"kind":"Name","value":"address"}},{"kind":"Field","name":{"kind":"Name","value":"address2"}},{"kind":"Field","name":{"kind":"Name","value":"postCode"}},{"kind":"Field","name":{"kind":"Name","value":"stateCode"}},{"kind":"Field","name":{"kind":"Name","value":"areaCode"}},{"kind":"Field","name":{"kind":"Name","value":"dealerCode"}},{"kind":"Field","name":{"kind":"Name","value":"gstRegistrationNo"}},{"kind":"Field","name":{"kind":"Name","value":"gstStatus"}},{"kind":"Field","name":{"kind":"Name","value":"panNo"}},{"kind":"Field","name":{"kind":"Name","value":"customerPriceGroup"}}]}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"totalCount"}}]}}]}}]} as unknown as DocumentNode<GetMyCustomersQuery, GetMyCustomersQueryVariables>;
export const GetDealersDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetDealers"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"department"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"take"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","alias":{"kind":"Name","value":"dealers"},"name":{"kind":"Name","value":"myDealers"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"entityType"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}}},{"kind":"Argument","name":{"kind":"Name","value":"entityCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"department"},"value":{"kind":"Variable","name":{"kind":"Name","value":"department"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}},{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"take"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"NullValue"}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","alias":{"kind":"Name","value":"items"},"name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","alias":{"kind":"Name","value":"phoneNo"},"name":{"kind":"Name","value":"mobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"depot"}},{"kind":"Field","name":{"kind":"Name","value":"eMail"}},{"kind":"Field","name":{"kind":"Name","value":"dealershipName"}}]}},{"kind":"Field","name":{"kind":"Name","value":"totalCount"}}]}}]}}]} as unknown as DocumentNode<GetDealersQuery, GetDealersQueryVariables>;
export const GetMyRegionsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetMyRegions"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"department"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"TerritoryFilterInput"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"myRegions"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"entityType"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}}},{"kind":"Argument","name":{"kind":"Name","value":"entityCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"department"},"value":{"kind":"Variable","name":{"kind":"Name","value":"department"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}},{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}}]}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"totalCount"}}]}}]}}]} as unknown as DocumentNode<GetMyRegionsQuery, GetMyRegionsQueryVariables>;
export const GetTransactionsPageDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetTransactionsPage"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"AccountTransactionSortInput"}}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"myTransactions"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"entityType"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}}},{"kind":"Argument","name":{"kind":"Name","value":"entityCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}},{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"totalCount"}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"date"}},{"kind":"Field","name":{"kind":"Name","value":"type"}},{"kind":"Field","name":{"kind":"Name","value":"documentNo"}},{"kind":"Field","name":{"kind":"Name","value":"amount"}},{"kind":"Field","name":{"kind":"Name","value":"customerNo"}},{"kind":"Field","name":{"kind":"Name","value":"customerName"}},{"kind":"Field","name":{"kind":"Name","value":"balance"}}]}}]}}]}}]} as unknown as DocumentNode<GetTransactionsPageQuery, GetTransactionsPageQueryVariables>;
export const GetMyTransactionsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetMyTransactions"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"myTransactions"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"entityType"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityType"}}},{"kind":"Argument","name":{"kind":"Name","value":"entityCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"entityCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}},{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"IntValue","value":"3"}},{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"ObjectValue","fields":[{"kind":"ObjectField","name":{"kind":"Name","value":"date"},"value":{"kind":"EnumValue","value":"DESC"}}]}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"date"}},{"kind":"Field","name":{"kind":"Name","value":"type"}},{"kind":"Field","name":{"kind":"Name","value":"documentNo"}},{"kind":"Field","name":{"kind":"Name","value":"amount"}},{"kind":"Field","name":{"kind":"Name","value":"customerNo"}},{"kind":"Field","name":{"kind":"Name","value":"customerName"}},{"kind":"Field","name":{"kind":"Name","value":"balance"}}]}}]}}]}}]} as unknown as DocumentNode<GetMyTransactionsQuery, GetMyTransactionsQueryVariables>;
export const GetProductionItemNosDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionItemNos"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionItemNos"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"minRate"}},{"kind":"Field","name":{"kind":"Name","value":"maxRate"}},{"kind":"Field","name":{"kind":"Name","value":"category"}}]}}]}}]} as unknown as DocumentNode<GetProductionItemNosQuery, GetProductionItemNosQueryVariables>;
export const GetProductionMakesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionMakes"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionMakes"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}}]}}]}}]} as unknown as DocumentNode<GetProductionMakesQuery, GetProductionMakesQueryVariables>;
export const GetProductionMakeSubMakeDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionMakeSubMake"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionMakeSubMake"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}}]}}]}}]} as unknown as DocumentNode<GetProductionMakeSubMakeQuery, GetProductionMakeSubMakeQueryVariables>;
export const GetProductionVendorsCodeNamesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionVendorsCodeNames"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionVendorsCodeNames"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}}]}}]}}]} as unknown as DocumentNode<GetProductionVendorsCodeNamesQuery, GetProductionVendorsCodeNamesQueryVariables>;
export const GetProductionInspectorCodeNamesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionInspectorCodeNames"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionInspectorCodeNames"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}}]}}]}}]} as unknown as DocumentNode<GetProductionInspectorCodeNamesQuery, GetProductionInspectorCodeNamesQueryVariables>;
export const GetProductionProcurementInspectionDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionProcurementInspection"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionProcurementInspection"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}}]}}]}}]} as unknown as DocumentNode<GetProductionProcurementInspectionQuery, GetProductionProcurementInspectionQueryVariables>;
export const GetProductionProcurementMarketsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionProcurementMarkets"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionProcurementMarkets"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}}]}}]}}]} as unknown as DocumentNode<GetProductionProcurementMarketsQuery, GetProductionProcurementMarketsQueryVariables>;
export const GetProductionVendorsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionVendors"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionVendors"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"address"}},{"kind":"Field","name":{"kind":"Name","value":"address2"}},{"kind":"Field","name":{"kind":"Name","value":"city"}},{"kind":"Field","name":{"kind":"Name","value":"category"}},{"kind":"Field","name":{"kind":"Name","value":"detail"}},{"kind":"Field","name":{"kind":"Name","value":"respCenter"}},{"kind":"Field","name":{"kind":"Name","value":"mobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"ecoMgrCode"}},{"kind":"Field","name":{"kind":"Name","value":"nameOnInvoice"}},{"kind":"Field","name":{"kind":"Name","value":"bankName"}},{"kind":"Field","name":{"kind":"Name","value":"bankIFSC"}},{"kind":"Field","name":{"kind":"Name","value":"bankAccNo"}},{"kind":"Field","name":{"kind":"Name","value":"postCode"}},{"kind":"Field","name":{"kind":"Name","value":"bankBranch"}},{"kind":"Field","name":{"kind":"Name","value":"selfInvoice"}},{"kind":"Field","name":{"kind":"Name","value":"panNo"}},{"kind":"Field","name":{"kind":"Name","value":"adhaarNo"}},{"kind":"Field","name":{"kind":"Name","value":"balance"}},{"kind":"Field","name":{"kind":"Name","value":"stateCode"}}]}}]}}]} as unknown as DocumentNode<GetProductionVendorsQuery, GetProductionVendorsQueryVariables>;
export const GetProductionEcomileLastNewNumberDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionEcomileLastNewNumber"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionEcomileLastNewNumber"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}}]}]}}]} as unknown as DocumentNode<GetProductionEcomileLastNewNumberQuery, GetProductionEcomileLastNewNumberQueryVariables>;
export const GetProductionProcurementOrdersInfoDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionProcurementOrdersInfo"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionProcurementOrdersInfo"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"orderNo"}},{"kind":"Field","name":{"kind":"Name","value":"supplier"}},{"kind":"Field","name":{"kind":"Name","value":"supplierCode"}},{"kind":"Field","name":{"kind":"Name","value":"respCenter"}},{"kind":"Field","name":{"kind":"Name","value":"status"}},{"kind":"Field","name":{"kind":"Name","value":"location"}},{"kind":"Field","name":{"kind":"Name","value":"managerCode"}},{"kind":"Field","name":{"kind":"Name","value":"date"}},{"kind":"Field","name":{"kind":"Name","value":"manager"}},{"kind":"Field","name":{"kind":"Name","value":"qty"}},{"kind":"Field","name":{"kind":"Name","value":"amount"}}]}}]}}]} as unknown as DocumentNode<GetProductionProcurementOrdersInfoQuery, GetProductionProcurementOrdersInfoQueryVariables>;
export const GetProductionProcurementOrderLinesDispatchDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionProcurementOrderLinesDispatch"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionProcurementOrderLinesDispatch"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"orderNo"}},{"kind":"Field","name":{"kind":"Name","value":"lineNo"}},{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"make"}},{"kind":"Field","name":{"kind":"Name","value":"serialNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchOrderNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchDate"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchDestination"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchVehicleNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchMobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchTransporter"}},{"kind":"Field","name":{"kind":"Name","value":"button"}},{"kind":"Field","name":{"kind":"Name","value":"model"}},{"kind":"Field","name":{"kind":"Name","value":"factInspection"}},{"kind":"Field","name":{"kind":"Name","value":"newSerialNo"}},{"kind":"Field","name":{"kind":"Name","value":"rejectionReason"}},{"kind":"Field","name":{"kind":"Name","value":"supplier"}},{"kind":"Field","name":{"kind":"Name","value":"location"}},{"kind":"Field","name":{"kind":"Name","value":"sortNo"}},{"kind":"Field","name":{"kind":"Name","value":"date"}},{"kind":"Field","name":{"kind":"Name","value":"inspection"}},{"kind":"Field","name":{"kind":"Name","value":"orderStatus"}},{"kind":"Field","name":{"kind":"Name","value":"inspector"}},{"kind":"Field","name":{"kind":"Name","value":"factInspector"}},{"kind":"Field","name":{"kind":"Name","value":"factInspectorFinal"}},{"kind":"Field","name":{"kind":"Name","value":"remark"}}]}}]}}]} as unknown as DocumentNode<GetProductionProcurementOrderLinesDispatchQuery, GetProductionProcurementOrderLinesDispatchQueryVariables>;
export const GetProductionProcurementOrderLinesNewNumberingDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionProcurementOrderLinesNewNumbering"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionProcurementOrderLinesNewNumbering"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"orderNo"}},{"kind":"Field","name":{"kind":"Name","value":"lineNo"}},{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"make"}},{"kind":"Field","name":{"kind":"Name","value":"serialNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchOrderNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchDate"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchDestination"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchVehicleNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchMobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchTransporter"}},{"kind":"Field","name":{"kind":"Name","value":"button"}},{"kind":"Field","name":{"kind":"Name","value":"model"}},{"kind":"Field","name":{"kind":"Name","value":"factInspection"}},{"kind":"Field","name":{"kind":"Name","value":"newSerialNo"}},{"kind":"Field","name":{"kind":"Name","value":"rejectionReason"}},{"kind":"Field","name":{"kind":"Name","value":"supplier"}},{"kind":"Field","name":{"kind":"Name","value":"location"}},{"kind":"Field","name":{"kind":"Name","value":"sortNo"}},{"kind":"Field","name":{"kind":"Name","value":"date"}},{"kind":"Field","name":{"kind":"Name","value":"inspection"}},{"kind":"Field","name":{"kind":"Name","value":"orderStatus"}},{"kind":"Field","name":{"kind":"Name","value":"inspector"}},{"kind":"Field","name":{"kind":"Name","value":"factInspector"}},{"kind":"Field","name":{"kind":"Name","value":"factInspectorFinal"}},{"kind":"Field","name":{"kind":"Name","value":"remark"}}]}}]}}]} as unknown as DocumentNode<GetProductionProcurementOrderLinesNewNumberingQuery, GetProductionProcurementOrderLinesNewNumberingQueryVariables>;
export const GetProductionProcurementOrderLinesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionProcurementOrderLines"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderInfoInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionProcurementOrderLines"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"lineNo"}},{"kind":"Field","name":{"kind":"Name","value":"vendorNo"}},{"kind":"Field","name":{"kind":"Name","value":"itemNo"}},{"kind":"Field","name":{"kind":"Name","value":"make"}},{"kind":"Field","name":{"kind":"Name","value":"serialNo"}},{"kind":"Field","name":{"kind":"Name","value":"amount"}},{"kind":"Field","name":{"kind":"Name","value":"subMake"}},{"kind":"Field","name":{"kind":"Name","value":"inspectorCode"}},{"kind":"Field","name":{"kind":"Name","value":"sortNo"}},{"kind":"Field","name":{"kind":"Name","value":"inspection"}},{"kind":"Field","name":{"kind":"Name","value":"inspector"}}]}}]}}]} as unknown as DocumentNode<GetProductionProcurementOrderLinesQuery, GetProductionProcurementOrderLinesQueryVariables>;
export const GetProductionProcurementDispatchOrdersDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionProcurementDispatchOrders"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionProcurementDispatchOrders"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"mobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"vehicleNo"}},{"kind":"Field","name":{"kind":"Name","value":"destination"}},{"kind":"Field","name":{"kind":"Name","value":"date"}},{"kind":"Field","name":{"kind":"Name","value":"tyres"}},{"kind":"Field","name":{"kind":"Name","value":"status"}}]}}]}}]} as unknown as DocumentNode<GetProductionProcurementDispatchOrdersQuery, GetProductionProcurementDispatchOrdersQueryVariables>;
export const GetProductionProcMarketsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionProcMarkets"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionProcMarkets"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}]}]}}]} as unknown as DocumentNode<GetProductionProcMarketsQuery, GetProductionProcMarketsQueryVariables>;
export const GetProductionEcomileProcurementTilesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionEcomileProcurementTiles"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionEcomileProcurementTiles"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"label"}},{"kind":"Field","name":{"kind":"Name","value":"value"}},{"kind":"Field","name":{"kind":"Name","value":"description"}}]}}]}}]} as unknown as DocumentNode<GetProductionEcomileProcurementTilesQuery, GetProductionEcomileProcurementTilesQueryVariables>;
export const GetProductionShipmentOrderForMergerDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProductionShipmentOrderForMerger"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionShipmentOrderForMerger"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"destination"}},{"kind":"Field","name":{"kind":"Name","value":"mobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"transport"}},{"kind":"Field","name":{"kind":"Name","value":"vehicleNo"}},{"kind":"Field","name":{"kind":"Name","value":"date"}}]}}]}}]} as unknown as DocumentNode<GetProductionShipmentOrderForMergerQuery, GetProductionShipmentOrderForMergerQueryVariables>;
export const UpdateProductionCasingDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateProductionCasing"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateProductionCasing"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"message"}}]}}]}}]} as unknown as DocumentNode<UpdateProductionCasingMutation, UpdateProductionCasingMutationVariables>;
export const InsertProductionCasingItemsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"InsertProductionCasingItems"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"casingItems"}},"type":{"kind":"NonNullType","type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"CasingItemInput"}}}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"insertProductionCasingItems"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"casingItems"},"value":{"kind":"Variable","name":{"kind":"Name","value":"casingItems"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"message"}}]}}]}}]} as unknown as DocumentNode<InsertProductionCasingItemsMutation, InsertProductionCasingItemsMutationVariables>;
export const UpdateProductionVendorDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateProductionVendor"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"VendorModelInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateProductionVendor"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"message"}}]}}]}}]} as unknown as DocumentNode<UpdateProductionVendorMutation, UpdateProductionVendorMutationVariables>;
export const CreateProductionVendorDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"CreateProductionVendor"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"createProductionVendor"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}]}]}}]} as unknown as DocumentNode<CreateProductionVendorMutation, CreateProductionVendorMutationVariables>;
export const NewProductionProcurementOrderDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"NewProductionProcurementOrder"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"newProductionProcurementOrder"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}]}]}}]} as unknown as DocumentNode<NewProductionProcurementOrderMutation, NewProductionProcurementOrderMutationVariables>;
export const UpdateProductionProcurementOrderDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateProductionProcurementOrder"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderInfoInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateProductionProcurementOrder"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}}]}]}}]} as unknown as DocumentNode<UpdateProductionProcurementOrderMutation, UpdateProductionProcurementOrderMutationVariables>;
export const NewProductionProcShipNoDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"NewProductionProcShipNo"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"newProductionProcShipNo"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}]}]}}]} as unknown as DocumentNode<NewProductionProcShipNoMutation, NewProductionProcShipNoMutationVariables>;
export const GenerateProductionGrAsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"GenerateProductionGRAs"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"param"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"FetchParamsInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"generateProductionGRAs"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"param"},"value":{"kind":"Variable","name":{"kind":"Name","value":"param"}}}]}]}}]} as unknown as DocumentNode<GenerateProductionGrAsMutation, GenerateProductionGrAsMutationVariables>;
export const InsertProductionProcurementOrderLineDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"InsertProductionProcurementOrderLine"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderLineInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"insertProductionProcurementOrderLine"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}}]}]}}]} as unknown as DocumentNode<InsertProductionProcurementOrderLineMutation, InsertProductionProcurementOrderLineMutationVariables>;
export const UpdateProductionProcurementOrderLineDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateProductionProcurementOrderLine"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderLineInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateProductionProcurementOrderLine"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}}]}]}}]} as unknown as DocumentNode<UpdateProductionProcurementOrderLineMutation, UpdateProductionProcurementOrderLineMutationVariables>;
export const UpdateProductionProcOrdLineDispatchDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateProductionProcOrdLineDispatch"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"lines"}},"type":{"kind":"NonNullType","type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderLineDispatchInput"}}}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateProductionProcOrdLineDispatch"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"lines"},"value":{"kind":"Variable","name":{"kind":"Name","value":"lines"}}}]}]}}]} as unknown as DocumentNode<UpdateProductionProcOrdLineDispatchMutation, UpdateProductionProcOrdLineDispatchMutationVariables>;
export const UpdateProductionProcOrdLineDispatchSingleDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateProductionProcOrdLineDispatchSingle"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"line"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderLineDispatchInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateProductionProcOrdLineDispatchSingle"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"line"},"value":{"kind":"Variable","name":{"kind":"Name","value":"line"}}}]}]}}]} as unknown as DocumentNode<UpdateProductionProcOrdLineDispatchSingleMutation, UpdateProductionProcOrdLineDispatchSingleMutationVariables>;
export const UpdateProductionProcOrdLineReceiptDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateProductionProcOrdLineReceipt"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"lines"}},"type":{"kind":"NonNullType","type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderLineDispatchInput"}}}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateProductionProcOrdLineReceipt"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"lines"},"value":{"kind":"Variable","name":{"kind":"Name","value":"lines"}}}]}]}}]} as unknown as DocumentNode<UpdateProductionProcOrdLineReceiptMutation, UpdateProductionProcOrdLineReceiptMutationVariables>;
export const UpdateProductionProcOrdLineRemoveDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateProductionProcOrdLineRemove"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"lines"}},"type":{"kind":"NonNullType","type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderLineDispatchInput"}}}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateProductionProcOrdLineRemove"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"lines"},"value":{"kind":"Variable","name":{"kind":"Name","value":"lines"}}}]}]}}]} as unknown as DocumentNode<UpdateProductionProcOrdLineRemoveMutation, UpdateProductionProcOrdLineRemoveMutationVariables>;
export const UpdateProductionProcOrdLineDropDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateProductionProcOrdLineDrop"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"lines"}},"type":{"kind":"NonNullType","type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderLineDispatchInput"}}}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"updateProductionProcOrdLineDrop"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"lines"},"value":{"kind":"Variable","name":{"kind":"Name","value":"lines"}}}]}]}}]} as unknown as DocumentNode<UpdateProductionProcOrdLineDropMutation, UpdateProductionProcOrdLineDropMutationVariables>;
export const DeleteProductionProcurementOrderLineDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"DeleteProductionProcurementOrderLine"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderLineInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"deleteProductionProcurementOrderLine"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}}]}]}}]} as unknown as DocumentNode<DeleteProductionProcurementOrderLineMutation, DeleteProductionProcurementOrderLineMutationVariables>;
export const DeleteProductionProcurementOrderDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"DeleteProductionProcurementOrder"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"OrderInfoInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"deleteProductionProcurementOrder"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}}]}]}}]} as unknown as DocumentNode<DeleteProductionProcurementOrderMutation, DeleteProductionProcurementOrderMutationVariables>;
export const GetMyVendorsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetMyVendors"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"categories"}},"type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"ecoMgr"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"before"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"last"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"VendorFilterInput"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"VendorSortInput"}}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"myVendors"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}},{"kind":"Argument","name":{"kind":"Name","value":"categories"},"value":{"kind":"Variable","name":{"kind":"Name","value":"categories"}}},{"kind":"Argument","name":{"kind":"Name","value":"ecoMgr"},"value":{"kind":"Variable","name":{"kind":"Name","value":"ecoMgr"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"before"},"value":{"kind":"Variable","name":{"kind":"Name","value":"before"}}},{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"last"},"value":{"kind":"Variable","name":{"kind":"Name","value":"last"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}},{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","alias":{"kind":"Name","value":"items"},"name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"city"}},{"kind":"Field","name":{"kind":"Name","value":"address"}},{"kind":"Field","name":{"kind":"Name","value":"address2"}},{"kind":"Field","name":{"kind":"Name","value":"postCode"}},{"kind":"Field","name":{"kind":"Name","value":"stateCode"}},{"kind":"Field","name":{"kind":"Name","value":"contact"}},{"kind":"Field","name":{"kind":"Name","value":"gstRegistrationNo"}},{"kind":"Field","name":{"kind":"Name","value":"gstStatus"}},{"kind":"Field","name":{"kind":"Name","value":"panNo"}},{"kind":"Field","name":{"kind":"Name","value":"nameOnInvoice"}},{"kind":"Field","name":{"kind":"Name","value":"bankName"}},{"kind":"Field","name":{"kind":"Name","value":"bankACNo"}},{"kind":"Field","name":{"kind":"Name","value":"bankIFSCCode"}},{"kind":"Field","name":{"kind":"Name","value":"bankBranch"}},{"kind":"Field","name":{"kind":"Name","value":"groupCategory"}},{"kind":"Field","name":{"kind":"Name","value":"groupDetails"}},{"kind":"Field","name":{"kind":"Name","value":"ecomileProcMgr"}},{"kind":"Field","name":{"kind":"Name","value":"territoryCode"}},{"kind":"Field","name":{"kind":"Name","value":"phoneNo"}},{"kind":"Field","alias":{"kind":"Name","value":"email"},"name":{"kind":"Name","value":"eMail"}},{"kind":"Field","name":{"kind":"Name","value":"responsibilityCenter"}},{"kind":"Field","name":{"kind":"Name","value":"blocked"}},{"kind":"Field","name":{"kind":"Name","value":"balance"}}]}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"hasPreviousPage"}},{"kind":"Field","name":{"kind":"Name","value":"startCursor"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"totalCount"}}]}}]}}]} as unknown as DocumentNode<GetMyVendorsQuery, GetMyVendorsQueryVariables>;
export const GetUserDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetUser"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"username"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"user"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"username"},"value":{"kind":"Variable","name":{"kind":"Name","value":"username"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"userId"}},{"kind":"Field","name":{"kind":"Name","value":"fullName"}},{"kind":"Field","name":{"kind":"Name","value":"rdpPassword"}},{"kind":"Field","name":{"kind":"Name","value":"navConfigName"}}]}}]}}]} as unknown as DocumentNode<GetUserQuery, GetUserQueryVariables>;
export const GetProfileDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProfile"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"profile"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"userId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userId"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"userId"}},{"kind":"Field","name":{"kind":"Name","value":"fullName"}},{"kind":"Field","name":{"kind":"Name","value":"userType"}},{"kind":"Field","name":{"kind":"Name","value":"mobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"email"}},{"kind":"Field","name":{"kind":"Name","value":"avatar"}},{"kind":"Field","name":{"kind":"Name","value":"lastPasswordChanged"}},{"kind":"Field","name":{"kind":"Name","value":"securityPIN"}},{"kind":"Field","name":{"kind":"Name","value":"entities"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"title"}},{"kind":"Field","name":{"kind":"Name","value":"location"}}]}}]}}]}}]} as unknown as DocumentNode<GetProfileQuery, GetProfileQueryVariables>;
export const SetProfileDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"SetProfile"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userId"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"input"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"ProfileUpdateInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"setProfile"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"userId"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userId"}}},{"kind":"Argument","name":{"kind":"Name","value":"input"},"value":{"kind":"Variable","name":{"kind":"Name","value":"input"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"message"}}]}}]}}]} as unknown as DocumentNode<SetProfileMutation, SetProfileMutationVariables>;
export const GetPostCodesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetPostCodes"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"PostCodeFilterInput"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"postCodes"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","alias":{"kind":"Name","value":"items"},"name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"city"}},{"kind":"Field","name":{"kind":"Name","value":"searchCity"}},{"kind":"Field","name":{"kind":"Name","value":"stateCode"}},{"kind":"Field","name":{"kind":"Name","value":"county"}},{"kind":"Field","name":{"kind":"Name","value":"countryRegionCode"}}]}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"hasPreviousPage"}},{"kind":"Field","name":{"kind":"Name","value":"startCursor"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"totalCount"}}]}}]}}]} as unknown as DocumentNode<GetPostCodesQuery, GetPostCodesQueryVariables>;
export const GetEcoprocTilesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetEcoprocTiles"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"statusPosted"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userDepartment"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userSpecialToken"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","alias":{"kind":"Name","value":"open"},"name":{"kind":"Name","value":"procurementOrders"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"statusFilter"},"value":{"kind":"IntValue","value":"0"}},{"kind":"Argument","name":{"kind":"Name","value":"userCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"userDepartment"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userDepartment"}}},{"kind":"Argument","name":{"kind":"Name","value":"userSpecialToken"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userSpecialToken"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenters"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"totalCount"}}]}},{"kind":"Field","alias":{"kind":"Name","value":"posted"},"name":{"kind":"Name","value":"procurementOrders"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"statusFilter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"statusPosted"}}},{"kind":"Argument","name":{"kind":"Name","value":"userCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"userDepartment"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userDepartment"}}},{"kind":"Argument","name":{"kind":"Name","value":"userSpecialToken"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userSpecialToken"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenters"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"totalCount"}}]}},{"kind":"Field","alias":{"kind":"Name","value":"vendors"},"name":{"kind":"Name","value":"myVendors"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"categories"},"value":{"kind":"ListValue","values":[{"kind":"StringValue","value":"CASING PROCUREMENT","block":false}]}},{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"totalCount"}}]}}]}}]} as unknown as DocumentNode<GetEcoprocTilesQuery, GetEcoprocTilesQueryVariables>;
export const GetProcurementOrdersDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProcurementOrders"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"PurchaseHeaderSortInput"}}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"PurchaseHeaderFilterInput"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userDepartment"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userSpecialToken"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"statusFilter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"procurementOrders"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}},{"kind":"Argument","name":{"kind":"Name","value":"userCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"userDepartment"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userDepartment"}}},{"kind":"Argument","name":{"kind":"Name","value":"userSpecialToken"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userSpecialToken"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenters"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}}},{"kind":"Argument","name":{"kind":"Name","value":"statusFilter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"statusFilter"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"totalCount"}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"postingDate"}},{"kind":"Field","name":{"kind":"Name","value":"orderDate"}},{"kind":"Field","name":{"kind":"Name","value":"buyFromVendorName"}},{"kind":"Field","name":{"kind":"Name","value":"buyFromVendorNo"}},{"kind":"Field","name":{"kind":"Name","value":"responsibilityCenter"}},{"kind":"Field","name":{"kind":"Name","value":"orderStatus"}},{"kind":"Field","name":{"kind":"Name","value":"ecomileProcMgr"}}]}}]}}]}}]} as unknown as DocumentNode<GetProcurementOrdersQuery, GetProcurementOrdersQueryVariables>;
export const GetProcurementOrderLinesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProcurementOrderLines"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"PurchaseLineSortInput"}}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"PurchaseLineFilterInput"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userCode"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userDepartment"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"userSpecialToken"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"statusFilter"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"procurementOrderLines"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}},{"kind":"Argument","name":{"kind":"Name","value":"userCode"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userCode"}}},{"kind":"Argument","name":{"kind":"Name","value":"userDepartment"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userDepartment"}}},{"kind":"Argument","name":{"kind":"Name","value":"userSpecialToken"},"value":{"kind":"Variable","name":{"kind":"Name","value":"userSpecialToken"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenters"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}}},{"kind":"Argument","name":{"kind":"Name","value":"statusFilter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"statusFilter"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"totalCount"}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"documentNo"}},{"kind":"Field","name":{"kind":"Name","value":"lineNo"}},{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"make"}},{"kind":"Field","name":{"kind":"Name","value":"serialNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchOrderNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchDate"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchDestination"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchVehicleNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchMobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchTransporter"}},{"kind":"Field","name":{"kind":"Name","value":"model"}},{"kind":"Field","name":{"kind":"Name","value":"inspection"}},{"kind":"Field","name":{"kind":"Name","value":"newSerialNo"}},{"kind":"Field","name":{"kind":"Name","value":"rejectionReason"}},{"kind":"Field","name":{"kind":"Name","value":"orderDate"}},{"kind":"Field","name":{"kind":"Name","value":"casingCondition"}},{"kind":"Field","name":{"kind":"Name","value":"orderStatus"}}]}}]}}]}}]} as unknown as DocumentNode<GetProcurementOrderLinesQuery, GetProcurementOrderLinesQueryVariables>;
export const GetEcomileLastNewNumberDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetEcomileLastNewNumber"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"productionEcomileLastNewNumber"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"respCenter"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenter"}}}]}]}}]} as unknown as DocumentNode<GetEcomileLastNewNumberQuery, GetEcomileLastNewNumberQueryVariables>;
export const GetProcurementNewNumberingPagedDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetProcurementNewNumberingPaged"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"first"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"after"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"fromDate"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"DateTime"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"toDate"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"DateTime"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"view"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"type"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"nos"}},"type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"ProcurementNewNumberingDtoFilterInput"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"ProcurementNewNumberingDtoSortInput"}}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"procurementNewNumberingPaged"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"first"},"value":{"kind":"Variable","name":{"kind":"Name","value":"first"}}},{"kind":"Argument","name":{"kind":"Name","value":"after"},"value":{"kind":"Variable","name":{"kind":"Name","value":"after"}}},{"kind":"Argument","name":{"kind":"Name","value":"fromDate"},"value":{"kind":"Variable","name":{"kind":"Name","value":"fromDate"}}},{"kind":"Argument","name":{"kind":"Name","value":"toDate"},"value":{"kind":"Variable","name":{"kind":"Name","value":"toDate"}}},{"kind":"Argument","name":{"kind":"Name","value":"respCenters"},"value":{"kind":"Variable","name":{"kind":"Name","value":"respCenters"}}},{"kind":"Argument","name":{"kind":"Name","value":"view"},"value":{"kind":"Variable","name":{"kind":"Name","value":"view"}}},{"kind":"Argument","name":{"kind":"Name","value":"type"},"value":{"kind":"Variable","name":{"kind":"Name","value":"type"}}},{"kind":"Argument","name":{"kind":"Name","value":"nos"},"value":{"kind":"Variable","name":{"kind":"Name","value":"nos"}}},{"kind":"Argument","name":{"kind":"Name","value":"where"},"value":{"kind":"Variable","name":{"kind":"Name","value":"where"}}},{"kind":"Argument","name":{"kind":"Name","value":"order"},"value":{"kind":"Variable","name":{"kind":"Name","value":"order"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"totalCount"}},{"kind":"Field","name":{"kind":"Name","value":"pageInfo"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"hasNextPage"}},{"kind":"Field","name":{"kind":"Name","value":"endCursor"}}]}},{"kind":"Field","name":{"kind":"Name","value":"nodes"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"orderNo"}},{"kind":"Field","name":{"kind":"Name","value":"lineNo"}},{"kind":"Field","name":{"kind":"Name","value":"no"}},{"kind":"Field","name":{"kind":"Name","value":"make"}},{"kind":"Field","name":{"kind":"Name","value":"serialNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchOrderNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchDate"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchDestination"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchVehicleNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchMobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"dispatchTransporter"}},{"kind":"Field","name":{"kind":"Name","value":"button"}},{"kind":"Field","name":{"kind":"Name","value":"model"}},{"kind":"Field","name":{"kind":"Name","value":"newSerialNo"}},{"kind":"Field","name":{"kind":"Name","value":"factInspection"}},{"kind":"Field","name":{"kind":"Name","value":"rejectionReason"}},{"kind":"Field","name":{"kind":"Name","value":"supplier"}},{"kind":"Field","name":{"kind":"Name","value":"location"}},{"kind":"Field","name":{"kind":"Name","value":"date"}},{"kind":"Field","name":{"kind":"Name","value":"inspector"}},{"kind":"Field","name":{"kind":"Name","value":"factInspector"}},{"kind":"Field","name":{"kind":"Name","value":"factInspectorFinal"}},{"kind":"Field","name":{"kind":"Name","value":"sortNo"}},{"kind":"Field","name":{"kind":"Name","value":"inspection"}},{"kind":"Field","name":{"kind":"Name","value":"orderStatus"}},{"kind":"Field","name":{"kind":"Name","value":"remark"}}]}}]}}]}}]} as unknown as DocumentNode<GetProcurementNewNumberingPagedQuery, GetProcurementNewNumberingPagedQueryVariables>;
export const GetRespCentersMockDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetRespCentersMock"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<GetRespCentersMockQuery, GetRespCentersMockQueryVariables>;
export const GetRespCenterMockDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetRespCenterMock"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<GetRespCenterMockQuery, GetRespCenterMockQueryVariables>;
export const GetUsersDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetUsers"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"skip"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"take"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"where"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"order"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"user"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"username"},"value":{"kind":"StringValue","value":"dummy","block":false}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"userId"}}]}}]}}]} as unknown as DocumentNode<GetUsersQuery, GetUsersQueryVariables>;
export const GetResponsibilityCentersDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetResponsibilityCenters"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"natureOfBusiness"}},"type":{"kind":"ListType","type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"skip"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"take"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<GetResponsibilityCentersQuery, GetResponsibilityCentersQueryVariables>;
export const GetEmployeesDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetEmployees"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"skip"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}},{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"take"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<GetEmployeesQuery, GetEmployeesQueryVariables>;
export const GetPermissionSetsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetPermissionSets"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"take"}},"type":{"kind":"NamedType","name":{"kind":"Name","value":"Int"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<GetPermissionSetsQuery, GetPermissionSetsQueryVariables>;
export const UpdateUserPermissionsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateUserPermissions"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<UpdateUserPermissionsMutation, UpdateUserPermissionsMutationVariables>;
export const UpdateUserResponsibilityCentersDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateUserResponsibilityCenters"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<UpdateUserResponsibilityCentersMutation, UpdateUserResponsibilityCentersMutationVariables>;
export const UpdateUserPostingSetupDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateUserPostingSetup"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<UpdateUserPostingSetupMutation, UpdateUserPostingSetupMutationVariables>;
export const UpdateUserDetailsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"UpdateUserDetails"},"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<UpdateUserDetailsMutation, UpdateUserDetailsMutationVariables>;
export const GetReportsDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetReports"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"category"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"__typename"}}]}}]} as unknown as DocumentNode<GetReportsQuery, GetReportsQueryVariables>;
export const GetDealerByCodeDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"query","name":{"kind":"Name","value":"GetDealerByCode"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"code"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"String"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"dealerByCode"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"code"},"value":{"kind":"Variable","name":{"kind":"Name","value":"code"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"code"}},{"kind":"Field","name":{"kind":"Name","value":"name"}},{"kind":"Field","name":{"kind":"Name","value":"dealershipName"}},{"kind":"Field","name":{"kind":"Name","value":"eMail"}},{"kind":"Field","name":{"kind":"Name","value":"mobileNo"}},{"kind":"Field","name":{"kind":"Name","value":"businessModel"}},{"kind":"Field","name":{"kind":"Name","value":"product"}},{"kind":"Field","name":{"kind":"Name","value":"status"}},{"kind":"Field","name":{"kind":"Name","value":"investmentAmount"}},{"kind":"Field","name":{"kind":"Name","value":"dealershipExpDate"}},{"kind":"Field","name":{"kind":"Name","value":"dealershipStartDate"}},{"kind":"Field","name":{"kind":"Name","value":"dateOfBirth"}},{"kind":"Field","name":{"kind":"Name","value":"dateOfAniversary"}},{"kind":"Field","name":{"kind":"Name","value":"brandedShop"}},{"kind":"Field","name":{"kind":"Name","value":"panNo"}},{"kind":"Field","name":{"kind":"Name","value":"gstNo"}},{"kind":"Field","name":{"kind":"Name","value":"aadharNo"}},{"kind":"Field","name":{"kind":"Name","value":"bankName"}},{"kind":"Field","name":{"kind":"Name","value":"bankACNo"}},{"kind":"Field","name":{"kind":"Name","value":"bankBranch"}},{"kind":"Field","name":{"kind":"Name","value":"bankIFSC"}}]}}]}}]} as unknown as DocumentNode<GetDealerByCodeQuery, GetDealerByCodeQueryVariables>;
export const SaveDealerDocument = {"kind":"Document","definitions":[{"kind":"OperationDefinition","operation":"mutation","name":{"kind":"Name","value":"SaveDealer"},"variableDefinitions":[{"kind":"VariableDefinition","variable":{"kind":"Variable","name":{"kind":"Name","value":"input"}},"type":{"kind":"NonNullType","type":{"kind":"NamedType","name":{"kind":"Name","value":"SaveDealerInput"}}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"saveDealer"},"arguments":[{"kind":"Argument","name":{"kind":"Name","value":"input"},"value":{"kind":"Variable","name":{"kind":"Name","value":"input"}}}],"selectionSet":{"kind":"SelectionSet","selections":[{"kind":"Field","name":{"kind":"Name","value":"success"}},{"kind":"Field","name":{"kind":"Name","value":"message"}}]}}]}}]} as unknown as DocumentNode<SaveDealerMutation, SaveDealerMutationVariables>;