export type BadRequestError = {
  [key: string]: string[];
};

export type BadRequest = {
  type: string;
  title: string;
  status: number;
  errors: BadRequestError;
};
