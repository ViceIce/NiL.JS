var obj = {
    prop: 42,
    toJSON: function () { return new Number(42) }
};
console.log(JSON.stringify([obj]))