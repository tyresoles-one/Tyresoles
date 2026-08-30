import { gql } from "graphql-request";

export const GET_EMAIL_ACCOUNTS = gql`
  query GetEmailAccounts {
    emailAccounts {
      success
      action
      error
      contacts {
        nickname
        email
        firstName
        lastName
        department
        designation
      }
    }
  }
`;

export const GET_EMAIL_ACCOUNT_DETAILS = gql`
  query GetEmailAccountDetails($userId: String, $employeeCode: String) {
    emailAccountDetails(userId: $userId, employeeCode: $employeeCode) {
      email
      fname
      sname
      nickname
      code
      day
      month
      year
      branch
      mobile
      city
      altemail
      status
      designation
      department
      role
      org_name
      url
      note
      timezone
      address
      state
      zip
      country_code
      ph_work
      ph_home
      fax
    }
  }
`;

export const GET_GLOBAL_ADDRESS_BOOK = gql`
  query GetGlobalAddressBook {
    globalAddressBook {
      success
      action
      error
      contacts {
        nickname
        email
        firstName
        lastName
        department
        designation
      }
    }
  }
`;

export const CREATE_EMAIL_ACCOUNT = gql`
  mutation CreateEmailAccount($input: CreateEmailAccountInput!) {
    createEmailAccount(input: $input) {
      success
      action
      message
      error
      contact {
        email
        fname
        sname
        nickname
      }
    }
  }
`;

export const UPDATE_EMAIL_ACCOUNT = gql`
  mutation UpdateEmailAccount($input: UpdateEmailAccountInput!) {
    updateEmailAccount(input: $input) {
      success
      action
      message
      error
    }
  }
`;

export const DELETE_EMAIL_ACCOUNT = gql`
  mutation DeleteEmailAccount($userEmail: String!) {
    deleteEmailAccount(userEmail: $userEmail) {
      success
      action
      message
      error
    }
  }
`;

export const CHANGE_EMAIL_ACCOUNT_PASSWORD = gql`
  mutation ChangeEmailAccountPassword($input: ChangeEmailPasswordInput!) {
    changeEmailAccountPassword(input: $input) {
      success
      action
      message
      error
    }
  }
`;

export const UPDATE_EMAIL_ACCOUNT_STATUS = gql`
  mutation UpdateEmailAccountStatus($input: UpdateEmailStatusInput!) {
    updateEmailAccountStatus(input: $input) {
      success
      action
      message
      error
    }
  }
`;

export const ADD_GLOBAL_ADDRESS_CONTACT = gql`
  mutation AddGlobalAddressContact($input: AddGlobalContactInput!) {
    addGlobalAddressContact(input: $input) {
      success
      action
      message
      error
    }
  }
`;
